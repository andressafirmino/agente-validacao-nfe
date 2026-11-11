using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Servicos;
using AuditoriaNFe.Domain.Calculos;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;
using Microsoft.Extensions.AI;

namespace AuditoriaNFe.Application.Ferramentas;

/// <summary>
/// Ferramentas de leitura do agente interno (contracts/ferramentas-agente.md v1). O estado da
/// execução vem de <see cref="EscopoFerramentasExecucao"/>.
/// </summary>
public sealed partial class FerramentasLeitura(ICatalogoRegras catalogo, IRelogio relogio, IGeradorIds ids)
{
    public IReadOnlyList<AIFunction> CriarFuncoes() =>
    [
        Criar(ListarDocumentos, PoliticaFerramentas.ListarDocumentos),
        Criar(ObterCamposExtraidos, PoliticaFerramentas.ObterCamposExtraidos),
        Criar(CalcularTotal, PoliticaFerramentas.CalcularTotal),
        Criar(ListarAchados, PoliticaFerramentas.ListarAchados),
    ];

    private static AIFunction Criar(Delegate metodo, string nome) =>
        AIFunctionFactory.Create(metodo.Method, metodo.Target, new AIFunctionFactoryOptions { Name = nome });

    [Description("Lista os documentos da base da sessão, com o estado das validações.")]
    public object ListarDocumentos()
    {
        var escopo = EscopoObrigatorio();
        return new
        {
            documentos = escopo.Estado.Documentos.Select(d => new
            {
                chaveAcesso = d.ChaveAcesso,
                arquivo = d.Arquivo,
                dataReferencia = d.DataReferencia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                origemDataReferencia = d.OrigemDataReferencia,
                pacoteAplicado = d.PacoteAplicado,
                validacaoLayout = TextoValidacao(d.ValidacaoLayout, d.MotivoValidacaoLayout),
                validacaoConteudo = TextoValidacao(d.ValidacaoConteudo, d.MotivoValidacaoConteudo),
                duplicadoDe = d.DuplicadoDe,
            }).ToList(),
        };
    }

    [Description("Devolve valores de campos extraídos de um documento da sessão.")]
    public object ObterCamposExtraidos(
        [Description("Chave de acesso com 44 dígitos.")] string chaveAcesso,
        [Description("Entre 1 e 20 campos do catálogo de extração.")] IReadOnlyList<string> campos)
    {
        if (!ChaveAcessoValida(chaveAcesso))
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.ArgumentoInvalido, "chaveAcesso inválida.");
        }

        if (campos is null || campos.Count is < 1 or > 20)
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.ArgumentoInvalido, "campos deve ter entre 1 e 20 itens.");
        }

        var escopo = EscopoObrigatorio();
        var documento = escopo.Estado.Documentos.FirstOrDefault(d => d.ChaveAcesso == chaveAcesso);
        if (documento is null)
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.DocumentoNaoEncontrado, "Documento não encontrado na sessão.");
        }

        foreach (var campo in campos)
        {
            if (!catalogo.CaminhosCatalogoExtracao.Contains(campo))
            {
                return ResultadoFerramenta.Erro(CodigoErroFerramenta.CampoNaoCatalogado, $"Campo não catalogado: {campo}.");
            }
        }

        var valores = campos.Select(campo =>
        {
            var extraido = documento.Campos.FirstOrDefault(c => string.Equals(c.Caminho, campo, StringComparison.Ordinal));
            string? valor;
            string caminho;
            if (extraido is null)
            {
                valor = null;
                caminho = campo;
            }
            else
            {
                caminho = extraido.Caminho;
                var classe = catalogo.ClassePorCaminhoExtracao.GetValueOrDefault(campo);
                valor = classe == "Pessoal" ? MinimizadorDadosIa.Mascarar(extraido.Valor) : extraido.Valor;
            }

            return new { campo, caminho, valor, item = ExtrairItem(caminho) };
        });

        return new { chaveAcesso, valores = valores.ToList() };
    }

    [Description("Soma um campo numérico catalogado sobre os documentos da sessão.")]
    public object CalcularTotal(
        [Description("Campo numérico do catálogo de extração.")] string campo,
        [Description("Restringe às chaves informadas; ausente considera todos.")] IReadOnlyList<string>? chavesAcesso = null,
        [Description("Incluir documentos com erro estrutural, com ressalva.")] bool incluirComErroEstrutural = false)
    {
        if (string.IsNullOrWhiteSpace(campo) || !catalogo.CaminhosCatalogoExtracao.Contains(campo))
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.CampoNaoCatalogado, "Campo não catalogado ou inválido.");
        }

        if (!catalogo.CaminhosNumericosExtracao.Contains(campo))
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.ArgumentoInvalido, "O campo não é numérico no catálogo.");
        }

        if (chavesAcesso is not null && (chavesAcesso.Count == 0 || chavesAcesso.Any(c => !ChaveAcessoValida(c))))
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.ArgumentoInvalido, "chavesAcesso inválidas.");
        }

        var escopo = EscopoObrigatorio();
        HashSet<string>? filtro = chavesAcesso is null ? null : chavesAcesso.ToHashSet(StringComparer.Ordinal);
        var entradas = escopo.Estado.Documentos.Select(d => new EntradaDocumentoCalculo(d.ChaveAcesso, d.Campos, d.SituacaoCalculo)).ToList();
        var id = ids.Novo();
        var registro = CalculadoraBase.SomarCampo(id, campo, entradas, filtro, incluirComErroEstrutural, relogio.Agora);
        escopo.Execucao.RegistrarCalculo(registro);

        return new
        {
            calculoId = registro.Id.ToString("D"),
            campo = registro.Campo,
            operacao = registro.Operacao.ToString(),
            resultado = registro.Resultado.ToString(CultureInfo.InvariantCulture),
            documentosIncluidos = registro.DocumentosIncluidos.Select(d => new { chaveAcesso = d.ChaveAcesso, comRessalva = d.ComRessalva }).ToList(),
            documentosExcluidos = registro.DocumentosExcluidos.Select(d => new { chaveAcesso = d.ChaveAcesso, motivo = d.Motivo.ToString() }).ToList(),
        };
    }

    [Description("Lista achados da execução e da base da sessão.")]
    public object ListarAchados(
        [Description("Filtra por chave de acesso com 44 dígitos.")] string? chaveAcesso = null,
        [Description("Uma das categorias de achado.")] string? categoria = null,
        [Description("Alta, Media, Baixa ou Informativa.")] string? severidade = null)
    {
        if (chaveAcesso is not null && !ChaveAcessoValida(chaveAcesso))
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.ArgumentoInvalido, "chaveAcesso inválida.");
        }

        if (categoria is not null && !Enum.TryParse<CategoriaAchado>(categoria, ignoreCase: false, out _))
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.ArgumentoInvalido, "categoria inválida.");
        }

        if (severidade is not null && !Enum.TryParse<Severidade>(severidade, ignoreCase: false, out _))
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.ArgumentoInvalido, "severidade inválida.");
        }

        var escopo = EscopoObrigatorio();
        var achados = escopo.Estado.Achados.AsEnumerable();
        if (chaveAcesso is not null)
        {
            achados = achados.Where(a => a.ChaveAcesso == chaveAcesso);
        }

        if (categoria is not null)
        {
            var cat = Enum.Parse<CategoriaAchado>(categoria);
            achados = achados.Where(a => a.Categoria == cat);
        }

        if (severidade is not null)
        {
            var sev = Enum.Parse<Severidade>(severidade);
            achados = achados.Where(a => a.Severidade == sev);
        }

        return new
        {
            achados = achados.Select(a => new
            {
                id = a.Id.ToString("D"),
                categoria = a.Categoria.ToString(),
                severidade = a.Severidade.ToString(),
                origem = a.Origem.ToString(),
                chaveAcesso = a.ChaveAcesso,
                campo = a.Campo,
                item = a.Item,
                valorInformado = a.ValorInformado,
                valorEsperado = a.ValorEsperado,
                regraId = a.RegraId,
                versaoRegra = a.VersaoRegra,
                pacoteSchema = a.PacoteSchema,
                descricao = a.Categoria == CategoriaAchado.ErroEstrutural
                    ? $"Violação de schema{(a.Campo is null ? string.Empty : $" em {a.Campo}")}."
                    : MinimizadorDadosIa.Mascarar(a.Descricao),
            }).ToList(),
        };
    }

    private static EscopoFerramentasExecucao EscopoObrigatorio() =>
        EscopoFerramentasExecucao.Atual ?? throw new InvalidOperationException("Escopo de ferramentas ausente.");

    private static bool ChaveAcessoValida(string chave) => Chave44().IsMatch(chave);

    private static string TextoValidacao(string situacao, string? motivo) =>
        situacao == nameof(SituacaoValidacao.Realizada) ? "Realizada" : $"NaoRealizada — {motivo ?? "motivo não informado"}";

    private static int? ExtrairItem(string caminho)
    {
        var match = ItemDet().Match(caminho);
        return match.Success && int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var item) ? item : null;
    }

    [GeneratedRegex(@"\A[0-9]{44}\z")]
    private static partial Regex Chave44();

    [GeneratedRegex(@"^det\[(\d+)\]/", RegexOptions.CultureInvariant)]
    private static partial Regex ItemDet();
}
