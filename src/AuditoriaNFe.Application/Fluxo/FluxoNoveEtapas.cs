using System.Globalization;
using System.Security.Cryptography;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Agente;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Ferramentas;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Application.Servicos;
using AuditoriaNFe.Domain.ContextoUsuario;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.Fluxo;

/// <summary>Arquivo recebido em <c>arquivos[]</c>; o conteúdo só existe durante a execução (FR-037a).</summary>
public sealed record ArquivoSolicitacao(string Nome, byte[] Conteudo, string? IdDocumento = null);

/// <summary>Etapa em andamento, para registrar a falha quando o tempo máximo se esgota (ERR-016).</summary>
public sealed class AndamentoFluxo(Execucao execucao)
{
    public Execucao Execucao { get; } = execucao ?? throw new ArgumentNullException(nameof(execucao));
    public int Etapa { get; private set; } = 1;
    public DateTimeOffset InicioEtapa { get; private set; }

    internal void Iniciar(int etapa, DateTimeOffset inicio)
    {
        Etapa = etapa;
        InicioEtapa = inicio;
    }
}

/// <summary>
/// As etapas 2 a 9 da constitution 3.4 sobre uma execução já recebida (etapa 1), na ordem do plano:
/// erros por arquivo não impedem os demais; conteúdo só para layout aprovado (FR-015); etapa 6 não
/// aplicável (sem ferramentas externas na v1). O resultado geral é do sistema, nunca da IA. A etapa 7
/// recebe também a base da sessão e o contexto do usuário; a etapa 9 atualiza o contexto do usuário
/// só com os resultados determinísticos (FR-057).
/// </summary>
public sealed partial class FluxoNoveEtapas(
    IProcessadorXml xml,
    ICatalogoRegras catalogo,
    IConsultaManuais manuais,
    IFerramentasExternas ferramentasExternas,
    IDocumentoRepositorio documentos,
    IExecucaoRepositorio execucoes,
    IContextoUsuarioRepositorio contextosUsuario,
    AtualizadorContextoUsuario atualizadorContextoUsuario,
    FabricaAgenteInterno fabricaAgente,
    ValidadorSaidaIa validadorSaida,
    IRelogio relogio,
    IGeradorIds ids,
    IOptions<McpOpcoes> mcp,
    IOptions<ContextoUsuarioOpcoes> contextoUsuario,
    ILogger<FluxoNoveEtapas> logger)
{
    public const string JustificativaMcpDesabilitado = "não aplicável — integração não habilitada";
    public const string JustificativaSemFerramentas = "não aplicável — integração de ferramentas externas não habilitada";
    public const string JustificativaFerramentasExternasConsultadas = "capacidades externas consultadas — nenhuma ação delegada na v1";
    public const string JustificativaSemArquivos = "não aplicável — nenhum arquivo enviado";
    public const string JustificativaRespostaForaEscopo = "resposta determinística — pergunta fora do escopo";
    public const string JustificativaIaSemDocumentos = "não aplicável — sem documentos analisados para análise por IA";
    public const string JustificativaValidacaoIaNaoExecutada = "não aplicável — análise por IA não executada";
    private const string Sistema = "sessão";
    private const string CaminhoCnpjEmitente = "emit/CNPJ";
    private const string CaminhoCnpjDestinatario = "dest/CNPJ";

    public async Task ExecutarAsync(
        AndamentoFluxo andamento,
        Sessao sessao,
        IReadOnlyList<ArquivoSolicitacao> arquivos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(andamento);
        ArgumentNullException.ThrowIfNull(sessao);
        ArgumentNullException.ThrowIfNull(arquivos);

        var execucao = andamento.Execucao;
        var estado = new EstadoProcessamento(execucao, sessao);

        if (arquivos.Count == 0)
        {
            await Etapa(andamento, 2, cancellationToken, () => new ResultadoEtapa(EstadoEtapa.NaoAplicavel, JustificativaSemArquivos));
            await Etapa(andamento, 3, cancellationToken, () => new ResultadoEtapa(EstadoEtapa.NaoAplicavel, JustificativaSemArquivos));
            await Etapa(andamento, 4, cancellationToken, () => new ResultadoEtapa(EstadoEtapa.NaoAplicavel, JustificativaSemArquivos));
        }
        else
        {
            await Etapa(andamento, 2, cancellationToken, () => ValidarFormato(estado, arquivos));
            await Etapa(andamento, 3, cancellationToken, () => IdentificarDocumentos(estado));
            await EtapaAsync(andamento, 4, cancellationToken, () => ValidarLayoutEConteudoAsync(estado, cancellationToken));
        }

        await EtapaAsync(andamento, 5, cancellationToken, () => ConsultarManuaisAsync(estado, cancellationToken));
        await EtapaAsync(andamento, 6, cancellationToken, () => ConsultarFerramentasExternasAsync(cancellationToken));

        if (AvaliadorEscopoPergunta.DeveResponderForaDoEscopoDeterministico(execucao.Tipo, execucao.Pergunta))
        {
            ResponderForaDoEscopoDeterministico(estado, andamento);
        }
        else if (DevePularAnaliseIa(estado))
        {
            PularAnaliseIa(andamento);
        }
        else
        {
            await AnalisarComIaAsync(estado, andamento, cancellationToken);
        }

        andamento.Iniciar(9, relogio.Agora);
        var inicio = relogio.Agora;
        execucao.RegistrarEtapa(9, EstadoEtapa.Executada, inicio, relogio.Agora);
        execucao.DefinirParecer(MontadorParecer.Montar(
            execucao,
            estado.Achados,
            estado.ValidacoesNaoRealizadas,
            estado.ErroDependencia is not null,
            estado.ResumoIa,
            xml.VersaoManifesto,
            catalogo.VersaoCatalogoRegras,
            semBaseAnalisada: !string.IsNullOrWhiteSpace(execucao.Pergunta) && sessao.Documentos.Count == 0));
        await atualizadorContextoUsuario.AtualizarAsync(execucao, Contribuicao(estado), cancellationToken);
        execucao.Concluir(relogio.Agora, estado.ErroDependencia);
    }

    /// <summary>CNPJs de emitente e destinatário de cada documento validado e os achados da execução.</summary>
    private static ContribuicaoExecucao Contribuicao(EstadoProcessamento estado) =>
        new(
            estado.Execucao.Id,
            estado.Sessao.Id,
            [.. estado.Arquivos.Where(a => a.Campos is not null).Select(a => new DocumentoAuditado(
                a.LayoutAprovado,
                a.Campos!.FirstOrDefault(c => c.Caminho == CaminhoCnpjEmitente)?.Valor,
                a.Campos!.FirstOrDefault(c => c.Caminho == CaminhoCnpjDestinatario)?.Valor))],
            estado.Achados);

    private ResultadoEtapa ValidarFormato(EstadoProcessamento estado, IReadOnlyList<ArquivoSolicitacao> arquivos)
    {
        foreach (var arquivo in arquivos)
        {
            var registro = new ArquivoRecebido(arquivo.Nome, arquivo.Conteudo.LongLength, Convert.ToHexStringLower(SHA256.HashData(arquivo.Conteudo)));
            registro.DefinirIdDocumento(arquivo.IdDocumento);
            estado.Execucao.RegistrarArquivo(registro);
            var documento = xml.Ler(arquivo.Conteudo);
            if (documento is null)
            {
                NaoRealizado(estado, registro, CodigoErro.XmlMalformado);
                continue;
            }

            estado.Arquivos.Add(new ArquivoEmAnalise(registro, documento));
        }

        return new ResultadoEtapa(EstadoEtapa.Executada);
    }

    private ResultadoEtapa IdentificarDocumentos(EstadoProcessamento estado)
    {
        if (estado.Arquivos.Count == 0)
        {
            return new ResultadoEtapa(EstadoEtapa.NaoAplicavel, "não aplicável — nenhum XML bem formado recebido");
        }

        var duplicidade = new DetectorDuplicidade(estado.Sessao.HashesPorChave);
        foreach (var arquivo in estado.Arquivos)
        {
            var identificacao = xml.Identificar(arquivo.Documento);
            arquivo.Identificacao = identificacao;
            arquivo.Registro.RegistrarIdentificacao(identificacao);
            if (!identificacao.Suportado)
            {
                var descricao = identificacao.DescreverMotivo()!;
                if (identificacao.MotivoNaoSuportado == MotivoNaoSuportado.VersaoNaoSuportada)
                {
                    NaoRealizado(estado, arquivo.Registro, CodigoErro.VersaoNaoSuportada, mensagem: MensagensErro.VersaoNaoSuportada(descricao));
                }
                else
                {
                    NaoRealizado(estado, arquivo.Registro, CodigoErro.DocumentoNaoSuportado, mensagem: MensagensErro.DocumentoNaoSuportado(descricao));
                }

                continue;
            }

            var repeticao = duplicidade.Registrar(arquivo.Registro.Nome, identificacao.ChaveAcesso!, arquivo.Registro.HashConteudo);
            switch (repeticao.Tipo)
            {
                case TipoDuplicidade.Duplicado:
                    var origem = repeticao.ArquivoAnterior ?? Sistema;
                    arquivo.Registro.MarcarDuplicado(origem);
                    arquivo.Registro.RegistrarValidacaoLayout(ValidacaoLayout.NaoRealizada(
                        $"documento duplicado de {origem}; analisado uma única vez", identificacao.DataReferencia, identificacao.OrigemDataReferencia));
                    estado.Achados.Add(new Achado(
                        ids.Novo(),
                        CategoriaAchado.AusenciaEvidencias,
                        Severidade.Informativa,
                        OrigemAchado.Sistema,
                        $"{CodigoErro.DocumentoDuplicado}: {MensagensErro.Para(CodigoErro.DocumentoDuplicado)} Repetição de {origem}.",
                        arquivo: arquivo.Registro.Nome,
                        chaveAcesso: identificacao.ChaveAcesso));
                    break;
                case TipoDuplicidade.ConteudoDivergente:
                    estado.Achados.Add(new Achado(
                        ids.Novo(),
                        CategoriaAchado.ErroEstrutural,
                        Severidade.Alta,
                        OrigemAchado.Sistema,
                        $"{CodigoErro.ConteudoDivergente}: {MensagensErro.Para(CodigoErro.ConteudoDivergente)} Mesma chave de {repeticao.ArquivoAnterior ?? Sistema}.",
                        arquivo: arquivo.Registro.Nome,
                        chaveAcesso: identificacao.ChaveAcesso));
                    arquivo.Analisar = true;
                    break;
                default:
                    arquivo.Analisar = true;
                    break;
            }
        }

        return new ResultadoEtapa(EstadoEtapa.Executada);
    }

    private async Task<ResultadoEtapa> ValidarLayoutEConteudoAsync(EstadoProcessamento estado, CancellationToken cancellationToken)
    {
        var analisar = estado.Arquivos.Where(a => a.Analisar).ToList();
        if (analisar.Count == 0)
        {
            return new ResultadoEtapa(EstadoEtapa.NaoAplicavel, "não aplicável — nenhum documento suportado para validar");
        }

        var hoje = DateOnly.FromDateTime(relogio.Agora.DateTime);
        foreach (var arquivo in analisar)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identificacao = arquivo.Identificacao!;
            if (identificacao.DataReferencia is not { } data || identificacao.OrigemDataReferencia is not { } origem)
            {
                NaoRealizado(estado, arquivo.Registro, CodigoErro.SchemaIndisponivelParaData);
                continue;
            }

            var layout = xml.ValidarLayout(arquivo.Documento, data, hoje);
            if (!layout.Encontrado)
            {
                NaoRealizado(estado, arquivo.Registro, CodigoErro.SchemaIndisponivelParaData, data, origem);
                continue;
            }

            arquivo.Registro.RegistrarValidacaoLayout(ValidacaoLayout.Realizada(
                data, origem, layout.PacoteVigente!.Id, layout.PacoteAplicado!.Id, layout.PacotesTentados, layout.EmConvivencia));
            arquivo.DataReferencia = data;
            arquivo.Campos = xml.Extrair(arquivo.Documento);

            if (layout.Erros.Count > 0)
            {
                estado.Achados.AddRange(layout.Erros.Select(e => Achado.ErroDeSchema(
                    ids.Novo(),
                    $"Violação do schema do pacote {e.Pacote}: {e.Mensagem}",
                    e.Pacote,
                    arquivo.Registro.Nome,
                    e.Linha,
                    e.Posicao,
                    identificacao.ChaveAcesso,
                    e.Caminho)));
                arquivo.Registro.RegistrarLayoutInvalido();
                estado.ValidacoesNaoRealizadas.Add(new ValidacaoNaoRealizada(arquivo.Registro.Nome, "Conteudo", ArquivoRecebido.MotivoLayoutInvalido));
            }
            else
            {
                var regras = catalogo.Aplicador.Aplicar(new DadosDocumento(arquivo.Registro.Nome, identificacao.ChaveAcesso!, data, arquivo.Campos), ids.Novo);
                estado.Achados.AddRange(regras.Achados);
                arquivo.Registro.RegistrarValidacaoConteudo(regras.RegrasAplicadas);
                arquivo.LayoutAprovado = true;
            }

            await GravarDocumentoAsync(estado, arquivo, layout.PacoteAplicado.Id, cancellationToken);
        }

        return new ResultadoEtapa(EstadoEtapa.Executada);
    }

    private async Task GravarDocumentoAsync(EstadoProcessamento estado, ArquivoEmAnalise arquivo, string pacote, CancellationToken cancellationToken)
    {
        var identificacao = arquivo.Identificacao!;
        var chave = identificacao.ChaveAcesso!;
        if (estado.Sessao.ClassificarDocumento(chave, arquivo.Registro.HashConteudo) != SituacaoDocumentoSessao.Novo)
        {
            return;
        }

        var documento = new DocumentoNFe(
            ids.Novo(),
            estado.Sessao.Id,
            estado.Execucao.ChaveIsolamento,
            chave,
            identificacao.ModeloIde!,
            identificacao.VersaoLayout!,
            identificacao.PossuiProtocolo,
            identificacao.DhEmi!,
            identificacao.DhRecbto,
            arquivo.DataReferencia!.Value,
            identificacao.OrigemDataReferencia!.Value,
            pacote,
            arquivo.Registro.HashConteudo,
            arquivo.Campos!,
            arquivo.Registro.Nome,
            estado.Execucao.Id);
        await documentos.InserirAsync(documento, cancellationToken);
        estado.Sessao.RegistrarDocumento(documento.Id, chave, arquivo.Registro.HashConteudo, relogio.Agora);
        arquivo.Registro.AssociarDocumento(documento.Id);
    }

    private async Task<ResultadoEtapa> ConsultarManuaisAsync(EstadoProcessamento estado, CancellationToken cancellationToken)
    {
        if (!manuais.Habilitada)
        {
            return new ResultadoEtapa(EstadoEtapa.NaoAplicavel, JustificativaMcpDesabilitado);
        }

        var porData = estado.Arquivos.Where(a => a.DataReferencia is not null).GroupBy(a => a.DataReferencia!.Value).OrderBy(g => g.Key).ToList();
        if (porData.Count == 0)
        {
            return new ResultadoEtapa(EstadoEtapa.NaoAplicavel, "não aplicável — nenhum documento analisado para consultar");
        }

        var recuperados = 0;
        try
        {
            foreach (var grupo in porData)
            {
                var nomes = grupo.Select(a => a.Registro.Nome).ToHashSet(StringComparer.Ordinal);
                var regras = estado.Achados
                    .Where(a => a.RegraId is not null && a.Arquivo is not null && nomes.Contains(a.Arquivo))
                    .Select(a => a.RegraId!)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList();
                var data = grupo.Key;
                var consulta = string.Format(
                    CultureInfo.InvariantCulture,
                    "Validação de totalizadores da NF-e modelo 55, versão de layout 4.00, vigente em {0:yyyy-MM-dd}.{1}",
                    data,
                    regras.Count == 0 ? string.Empty : $" Regras relacionadas: {string.Join(", ", regras)}.");
                var resultado = await manuais.ConsultarAsync(
                    new ConsultaManuais("NF-e", IdentificacaoDocumento.ModeloSuportado, IdentificacaoDocumento.VersaoSuportada, data, consulta, regras, mcp.Value.QuantidadeMaximaTrechos),
                    cancellationToken);

                foreach (var trecho in resultado.Trechos.Where(t => Valido(t, data)))
                {
                    recuperados++;
                    if (estado.Trechos.TryAdd(trecho.ChunkId, trecho))
                    {
                        estado.Execucao.RegistrarTrechoConsultado(new TrechoConsultado(trecho.ChunkId, trecho.Documento, trecho.Versao));
                    }
                }
            }
        }
        catch (Exception erro) when (erro is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFalhaManuais(logger, estado.Execucao.Id, erro.GetType().Name);
            FalhaDependencia(estado, CodigoErro.BaseConhecimentoIndisponivel);
            return new ResultadoEtapa(EstadoEtapa.Falhou, MensagensErro.Para(CodigoErro.BaseConhecimentoIndisponivel));
        }

        if (recuperados == 0)
        {
            estado.Achados.Add(new Achado(
                ids.Novo(),
                CategoriaAchado.AusenciaEvidencias,
                Severidade.Informativa,
                OrigemAchado.Sistema,
                "Nenhum trecho de manual oficial foi recuperado para a versão e a data de referência dos documentos analisados."));
        }

        return new ResultadoEtapa(EstadoEtapa.Executada);
    }

    private async Task<ResultadoEtapa> ConsultarFerramentasExternasAsync(CancellationToken cancellationToken)
    {
        if (!ferramentasExternas.Habilitada)
        {
            return new ResultadoEtapa(EstadoEtapa.NaoAplicavel, JustificativaSemFerramentas);
        }

        try
        {
            _ = await ferramentasExternas.ConsultarCapacidadesAsync(cancellationToken).ConfigureAwait(false);
            return new ResultadoEtapa(EstadoEtapa.Executada, JustificativaFerramentasExternasConsultadas);
        }
        catch (Exception erro) when (erro is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFalhaFerramentasExternas(logger, erro.GetType().Name);
            return new ResultadoEtapa(
                EstadoEtapa.NaoAplicavel,
                "não aplicável — consulta de ferramentas externas indisponível");
        }
    }

    private static bool DevePularAnaliseIa(EstadoProcessamento estado) =>
        estado.Execucao.Tipo == TipoExecucao.AnaliseLote && !estado.Arquivos.Any(a => a.LayoutAprovado);

    private void ResponderForaDoEscopoDeterministico(EstadoProcessamento estado, AndamentoFluxo andamento)
    {
        var texto = MensagensErro.Para(CodigoErro.ForaDoEscopo);
        estado.Execucao.DefinirRespostaPergunta(new RespostaPerguntaExecucao(texto, [], [], informacaoIndisponivel: false, foraDeEscopo: true));
        var inicio = relogio.Agora;
        andamento.Iniciar(7, inicio);
        var fim = relogio.Agora;
        estado.Execucao.RegistrarEtapa(7, EstadoEtapa.Executada, inicio, fim, JustificativaRespostaForaEscopo);
        estado.Execucao.RegistrarEtapa(8, EstadoEtapa.Executada, fim, relogio.Agora);
    }

    private void PularAnaliseIa(AndamentoFluxo andamento)
    {
        var inicio = relogio.Agora;
        andamento.Iniciar(7, inicio);
        andamento.Execucao.RegistrarEtapa(7, EstadoEtapa.NaoAplicavel, justificativa: JustificativaIaSemDocumentos);
        andamento.Execucao.RegistrarEtapa(8, EstadoEtapa.NaoAplicavel, justificativa: JustificativaValidacaoIaNaoExecutada);
    }

    private async Task AnalisarComIaAsync(EstadoProcessamento estado, AndamentoFluxo andamento, CancellationToken cancellationToken)
    {
        andamento.Iniciar(7, relogio.Agora);
        var inicio = relogio.Agora;
        var ferramentasEstado = await MontarEstadoFerramentasAsync(estado, cancellationToken);
        using var escopoFerramentas = EscopoFerramentasExecucao.Iniciar(estado.Execucao, ferramentasEstado);
        var contexto = new ContextoAgenteIa(
            MontarContexto(estado),
            await MontarContextoSessaoAsync(estado, cancellationToken),
            estado.Execucao.Tipo,
            estado.Execucao.Calculos,
            await MontarContextoUsuarioAsync(estado.Execucao, cancellationToken));
        var resultado = await validadorSaida.ObterSaidaAsync(fabricaAgente.Criar(contexto), contexto, estado.Sessao.HistoricoAgente, cancellationToken);
        var fim = relogio.Agora;
        if (resultado.Modelo is not null)
        {
            estado.Execucao.DefinirModelo(resultado.Modelo);
        }

        if (resultado.HistoricoAgente is not null)
        {
            estado.Sessao.AtualizarHistoricoAgente(resultado.HistoricoAgente, fim);
        }

        switch (resultado.Situacao)
        {
            case SituacaoAnaliseIa.Valida:
                estado.Execucao.RegistrarEtapa(7, EstadoEtapa.Executada, inicio, fim);
                estado.Execucao.RegistrarEtapa(8, EstadoEtapa.Executada, fim, relogio.Agora);
                IncorporarSaida(estado, resultado.Saida!, contexto);
                break;
            case SituacaoAnaliseIa.RespostaInvalida:
                estado.Execucao.RegistrarEtapa(7, EstadoEtapa.Executada, inicio, fim);
                estado.Execucao.RegistrarEtapa(8, EstadoEtapa.Falhou, fim, relogio.Agora, MensagensErro.Para(CodigoErro.RespostaIaInvalida));
                FalhaDependencia(estado, CodigoErro.RespostaIaInvalida);
                break;
            default:
                estado.Execucao.RegistrarEtapa(7, EstadoEtapa.Falhou, inicio, fim, MensagensErro.Para(CodigoErro.ProvedorIaIndisponivel));
                estado.Execucao.RegistrarEtapa(8, EstadoEtapa.NaoExecutada, justificativa: "Não executada: não há saída do agente para validar.");
                FalhaDependencia(estado, CodigoErro.ProvedorIaIndisponivel);
                break;
        }
    }

    private ContextoExecucaoIa MontarContexto(EstadoProcessamento estado)
    {
        var regras = Regras(estado.Execucao.Arquivos.SelectMany(a => a.RegrasAplicadas).Select(r => (r.RegraId, r.VersaoRegra)).ToHashSet());
        var documentos = estado.Execucao.Arquivos.Select(registro =>
        {
            var campos = estado.Arquivos.FirstOrDefault(a => ReferenceEquals(a.Registro, registro))?.Campos ?? [];
            return new DocumentoContextoIa(
                MinimizadorDadosIa.Mascarar(registro.Nome),
                registro.ChaveAcesso,
                registro.ValidacaoLayout.DataReferencia,
                registro.ValidacaoLayout.PacoteAplicado,
                registro.ValidacaoLayout.Estado.ToString(),
                registro.ValidacaoConteudo.ToString(),
                MinimizadorDadosIa.Campos(campos));
        }).ToList();

        return new ContextoExecucaoIa(
            documentos,
            [.. estado.Achados.Select(MinimizadorDadosIa.Achado)],
            regras,
            [.. estado.Execucao.Etapas.Where(e => e.Registrada).Select(e => new EtapaContextoIa(e.Numero, e.Nome, e.Estado.ToString()))],
            [.. estado.Trechos.Values.Select(t => new TrechoContextoIa(t.ChunkId, t.Documento, t.Versao, MinimizadorDadosIa.Mascarar(t.Trecho)))],
            estado.Execucao.Pergunta is null ? null : MinimizadorDadosIa.Mascarar(estado.Execucao.Pergunta));
    }

    /// <summary>
    /// Documentos e achados determinísticos das execuções anteriores da sessão, lidos do armazenamento
    /// (FR-037, FR-042). Sessão sem nenhum documento analisado é informada ao agente.
    /// </summary>
    private async Task<ContextoSessaoIa> MontarContextoSessaoAsync(EstadoProcessamento estado, CancellationToken cancellationToken)
    {
        var sessao = estado.Sessao;
        var semBase = sessao.Documentos.Count == 0;
        if (sessao.Execucoes.All(id => id == estado.Execucao.Id))
        {
            return ContextoSessaoIa.Nova(semBase);
        }

        var anteriores = (await execucoes.ListarPorSessaoAsync(sessao.Id, sessao.ChaveIsolamento, cancellationToken))
            .Where(e => e.Id != estado.Execucao.Id)
            .OrderBy(e => e.IniciadaEm)
            .ToList();
        var documentosPorId = (await documentos.ListarPorSessaoAsync(sessao.Id, sessao.ChaveIsolamento, cancellationToken))
            .ToDictionary(d => d.Id);

        var documentosAnteriores = anteriores
            .SelectMany(e => e.Arquivos)
            .Where(a => a.DocumentoId is { } id && documentosPorId.ContainsKey(id))
            .Select(a => new DocumentoContextoIa(
                MinimizadorDadosIa.Mascarar(a.Nome),
                a.ChaveAcesso,
                a.ValidacaoLayout.DataReferencia,
                a.ValidacaoLayout.PacoteAplicado,
                a.ValidacaoLayout.Estado.ToString(),
                a.ValidacaoConteudo.ToString(),
                MinimizadorDadosIa.Campos(documentosPorId[a.DocumentoId!.Value].DadosExtraidos)))
            .ToList();
        var achadosAnteriores = anteriores
            .Where(e => e.Parecer is not null)
            .SelectMany(e => e.Parecer!.Achados)
            .Where(a => a.Origem == OrigemAchado.Deterministica)
            .Select(MinimizadorDadosIa.Achado)
            .ToList();
        var aplicadas = anteriores.SelectMany(e => e.Arquivos).SelectMany(a => a.RegrasAplicadas).Select(r => (r.RegraId, r.VersaoRegra)).ToHashSet();

        return new ContextoSessaoIa(anteriores.Count, documentosAnteriores, achadosAnteriores, Regras(aplicadas), semBase);
    }

    /// <summary>Só para usuário identificado; contexto vencido é ignorado pelo repositório (FR-060).</summary>
    private async Task<ContextoUsuarioIa?> MontarContextoUsuarioAsync(Execucao execucao, CancellationToken cancellationToken)
    {
        if (!execucao.UsuarioIdentificado)
        {
            return null;
        }

        var contexto = await contextosUsuario.ObterAsync(execucao.ChaveIsolamento, relogio.Agora, cancellationToken);
        return contexto is null ? null : ContextoUsuarioIa.De(contexto, contextoUsuario.Value.MaxItensNoPrompt);
    }

    private List<RegraContextoIa> Regras(HashSet<(string RegraId, int VersaoRegra)> aplicadas) =>
        catalogo.Regras
            .Where(r => aplicadas.Contains((r.Id, r.VersaoRegra)))
            .Select(r => new RegraContextoIa(r.Id, r.VersaoRegra, MinimizadorDadosIa.Mascarar(r.Descricao)))
            .ToList();

    private void IncorporarSaida(EstadoProcessamento estado, ContratoIaV1 saida, ContextoAgenteIa contexto)
    {
        var arquivoPorChave = estado.Execucao.Arquivos
            .Where(a => a.ChaveAcesso is not null)
            .GroupBy(a => a.ChaveAcesso!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Nome, StringComparer.Ordinal);

        foreach (var indicio in saida.Indicios)
        {
            var evidencias = indicio.Evidencias.Select(e => Evidencia(e, contexto, estado)).ToList();
            var chave = indicio.Evidencias.Select(e => e.ChaveAcesso).FirstOrDefault(c => c is not null && arquivoPorChave.ContainsKey(c));
            estado.Achados.Add(new Achado(
                ids.Novo(),
                CategoriaAchado.IndicioIa,
                Enum.Parse<Severidade>(indicio.Severidade),
                OrigemAchado.IA,
                indicio.Descricao,
                arquivo: chave is null ? null : arquivoPorChave[chave],
                chaveAcesso: chave,
                evidencias: evidencias));
        }

        foreach (var ausencia in saida.AusenciasEvidencia)
        {
            estado.Achados.Add(new Achado(
                ids.Novo(),
                CategoriaAchado.AusenciaEvidencias,
                Severidade.Informativa,
                OrigemAchado.IA,
                $"{ausencia.PontoAvaliado}: {ausencia.Motivo}"));
        }

        estado.ResumoIa = saida.Resumo;
        if (contexto.EhPergunta && saida.Resposta is not null)
        {
            estado.Execucao.DefinirRespostaPergunta(MontadorResposta.De(saida));
        }
    }

    private async Task<FerramentasEstadoExecucao> MontarEstadoFerramentasAsync(EstadoProcessamento estado, CancellationToken cancellationToken)
    {
        var visoes = new List<DocumentoFerramenta>();
        var persistidos = await documentos.ListarPorSessaoAsync(estado.Sessao.Id, estado.Execucao.ChaveIsolamento, cancellationToken);
        var porChave = persistidos.ToDictionary(d => d.ChaveAcesso, StringComparer.Ordinal);
        foreach (var arquivo in estado.Arquivos)
        {
            if (arquivo.Identificacao?.ChaveAcesso is not { } chave)
            {
                continue;
            }

            var campos = arquivo.Campos ?? [];
            if (porChave.TryGetValue(chave, out var existente))
            {
                visoes.Add(MontadorEstadoFerramentas.DePersistido(existente, arquivo.Registro));
            }
            else
            {
                visoes.Add(MontadorEstadoFerramentas.DeArquivo(arquivo.Registro, campos));
            }
        }

        foreach (var doc in persistidos)
        {
            if (visoes.Any(d => d.ChaveAcesso == doc.ChaveAcesso))
            {
                continue;
            }

            visoes.Add(MontadorEstadoFerramentas.DePersistido(doc));
        }

        var achados = new List<Achado>(estado.Achados);
        var anteriores = (await execucoes.ListarPorSessaoAsync(estado.Sessao.Id, estado.Execucao.ChaveIsolamento, cancellationToken))
            .Where(e => e.Id != estado.Execucao.Id && e.Parecer is not null)
            .OrderBy(e => e.IniciadaEm);
        foreach (var exec in anteriores)
        {
            achados.AddRange(exec.Parecer!.Achados.Where(a => a.Origem == OrigemAchado.Deterministica));
        }

        return MontadorEstadoFerramentas.Montar(visoes, achados, estado.Execucao.Calculos.ToList());
    }

    private Evidencia Evidencia(EvidenciaIaV1 evidencia, ContextoAgenteIa contexto, EstadoProcessamento estado) => evidencia.Tipo switch
    {
        "CampoXml" => Domain.Validacao.Evidencia.CampoXml(evidencia.ChaveAcesso!, evidencia.Caminho!, evidencia.Valor!),
        "RegraCatalogo" => Domain.Validacao.Evidencia.RegraCatalogo(
            evidencia.RegraId!,
            contexto.Regras.Where(r => r.RegraId == evidencia.RegraId).Max(r => r.VersaoRegra),
            catalogo.VersaoCatalogoRegras),
        _ => estado.Trechos[evidencia.ChunkId!] is var trecho
            ? Domain.Validacao.Evidencia.TrechoManual(trecho.ChunkId, trecho.Documento, trecho.Versao, trecho.Trecho)
            : throw new InvalidOperationException("Evidência sem trecho."),
    };

    private void FalhaDependencia(EstadoProcessamento estado, string codigo)
    {
        estado.ErroDependencia ??= new ErroExecucao(codigo, MensagensErro.Para(codigo));
        estado.Achados.Add(new Achado(
            ids.Novo(),
            CategoriaAchado.FalhaTecnica,
            Severidade.Media,
            OrigemAchado.Sistema,
            $"{codigo}: {MensagensErro.Para(codigo)}"));
    }

    private static void NaoRealizado(
        EstadoProcessamento estado,
        ArquivoRecebido registro,
        string codigo,
        DateOnly? dataReferencia = null,
        OrigemDataReferencia? origem = null,
        string? mensagem = null)
    {
        var motivo = $"{codigo}: {mensagem ?? MensagensErro.Para(codigo)}";
        registro.RegistrarValidacaoLayout(ValidacaoLayout.NaoRealizada(motivo, dataReferencia, origem));
        estado.ValidacoesNaoRealizadas.Add(new ValidacaoNaoRealizada(registro.Nome, "Layout", motivo));
        estado.ValidacoesNaoRealizadas.Add(new ValidacaoNaoRealizada(registro.Nome, "Conteudo", motivo));
    }

    private static bool Valido(TrechoManual trecho, DateOnly data) =>
        !string.IsNullOrWhiteSpace(trecho.ChunkId)
        && !string.IsNullOrWhiteSpace(trecho.Documento)
        && !string.IsNullOrWhiteSpace(trecho.Versao)
        && !string.IsNullOrWhiteSpace(trecho.Trecho)
        && trecho.VigenciaInicio <= data
        && (trecho.VigenciaFim is not { } fim || data <= fim);

    private Task Etapa(AndamentoFluxo andamento, int numero, CancellationToken cancellationToken, Func<ResultadoEtapa> executar) =>
        EtapaAsync(andamento, numero, cancellationToken, () => Task.FromResult(executar()));

    private async Task EtapaAsync(AndamentoFluxo andamento, int numero, CancellationToken cancellationToken, Func<Task<ResultadoEtapa>> executar)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inicio = relogio.Agora;
        andamento.Iniciar(numero, inicio);
        var resultado = await executar();
        andamento.Execucao.RegistrarEtapa(
            numero,
            resultado.Estado,
            resultado.Estado is EstadoEtapa.NaoAplicavel or EstadoEtapa.NaoExecutada ? null : inicio,
            resultado.Estado is EstadoEtapa.NaoAplicavel or EstadoEtapa.NaoExecutada ? null : relogio.Agora,
            resultado.Justificativa);
    }

    [LoggerMessage(EventId = 1521, Level = LogLevel.Warning,
        Message = "Consulta a manuais indisponível na execução {ExecucaoId} ({TipoErro}).")]
    private static partial void LogFalhaManuais(ILogger logger, Guid execucaoId, string tipoErro);

    [LoggerMessage(EventId = 1522, Level = LogLevel.Warning,
        Message = "Consulta de ferramentas externas indisponível ({TipoErro}).")]
    private static partial void LogFalhaFerramentasExternas(ILogger logger, string tipoErro);

    private sealed record ResultadoEtapa(EstadoEtapa Estado, string? Justificativa = null);

    private sealed class ArquivoEmAnalise(ArquivoRecebido registro, DocumentoXmlLido documento)
    {
        public ArquivoRecebido Registro { get; } = registro;
        public DocumentoXmlLido Documento { get; } = documento;
        public IdentificacaoDocumento? Identificacao { get; set; }
        public bool Analisar { get; set; }
        public DateOnly? DataReferencia { get; set; }
        public IReadOnlyList<CampoExtraido>? Campos { get; set; }
        public bool LayoutAprovado { get; set; }
    }

    private sealed class EstadoProcessamento(Execucao execucao, Sessao sessao)
    {
        public Execucao Execucao { get; } = execucao;
        public Sessao Sessao { get; } = sessao;
        public List<ArquivoEmAnalise> Arquivos { get; } = [];
        public List<Achado> Achados { get; } = [];
        public List<ValidacaoNaoRealizada> ValidacoesNaoRealizadas { get; } = [];
        public Dictionary<string, TrechoManual> Trechos { get; } = new(StringComparer.Ordinal);
        public ErroExecucao? ErroDependencia { get; set; }
        public string? ResumoIa { get; set; }
    }
}
