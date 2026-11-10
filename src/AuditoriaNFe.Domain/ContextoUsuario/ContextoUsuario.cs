using System.Text.RegularExpressions;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.ContextoUsuario;

/// <summary>
/// Informações próprias da auditoria mantidas sobre um usuário entre sessões (data-model §13; FR-055
/// a FR-060). Só CNPJs com contagem, chaves de achados recorrentes e preferências declaradas, cada um
/// com origem: não há onde gravar CPF, nomes, endereços, valores de documentos nem texto livre.
/// Não existe para "anonimo" (FR-052c).
/// </summary>
public sealed partial class ContextoUsuario
{
    /// <summary>Quantas execuções ficam registradas para impedir contagem repetida.</summary>
    public const int MaxExecucoesContabilizadas = 200;

    /// <summary>Chave de <see cref="AchadoRecorrente"/> para violações do schema, que não têm <c>regraId</c>.</summary>
    public const string ChaveViolacaoSchema = "violacao-schema";

    private List<CnpjAuditado> _cnpjsAuditados = [];
    private List<AchadoRecorrente> _achadosRecorrentes = [];
    private List<Guid> _execucoesContabilizadas = [];

    private ContextoUsuario()
    {
        Id = string.Empty;
        UsuarioId = string.Empty;
        ChamadorId = string.Empty;
    }

    /// <summary><c>_id</c>: a chave de isolamento (FR-056).</summary>
    public string Id { get; private set; }

    /// <summary>Dado pessoal: nunca em log.</summary>
    public string UsuarioId { get; private set; }

    public string ChamadorId { get; private set; }

    public IReadOnlyList<CnpjAuditado> CnpjsAuditados => _cnpjsAuditados;

    public IReadOnlyList<AchadoRecorrente> AchadosRecorrentes => _achadosRecorrentes;

    public PreferenciasUsuario? Preferencias { get; private set; }

    /// <summary>Mais recentes por último, limitadas a <see cref="MaxExecucoesContabilizadas"/>.</summary>
    public IReadOnlyList<Guid> ExecucoesContabilizadas => _execucoesContabilizadas;

    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }
    public DateTimeOffset UltimaAtividadeEm { get; private set; }

    /// <summary><c>ultimaAtividadeEm</c> + <c>ContextoUsuario:RetencaoInatividade</c>.</summary>
    public DateTimeOffset ExpiraEm { get; private set; }

    public string ChaveIsolamento => Id;

    public static ContextoUsuario Criar(string chaveIsolamento, string usuarioId, string chamadorId, DateTimeOffset agora, LimitesContextoUsuario limites)
    {
        ArgumentNullException.ThrowIfNull(limites);
        if (chaveIsolamento is null || !Sha256Hex().IsMatch(chaveIsolamento))
        {
            throw new ArgumentException("Chave de isolamento inválida.", nameof(chaveIsolamento));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(chamadorId);
        if (usuarioId == IdentificacaoSolicitante.UsuarioAnonimo)
        {
            throw new InvalidOperationException("Usuário não identificado não tem contexto (FR-052c).");
        }

        var contexto = new ContextoUsuario
        {
            Id = chaveIsolamento,
            UsuarioId = usuarioId,
            ChamadorId = chamadorId,
            CriadoEm = agora,
        };
        contexto.Renovar(agora, limites);
        return contexto;
    }

    /// <summary>Vencido mesmo antes da remoção pelo TTL: a leitura o ignora.</summary>
    public bool ExpiradoEm(DateTimeOffset agora) => agora >= ExpiraEm;

    public bool JaContabilizou(Guid execucaoId) => _execucoesContabilizadas.Contains(execucaoId);

    /// <summary>
    /// Acrescenta os CNPJs dos documentos aprovados no layout e os achados determinísticos de
    /// <c>ErroEstrutural</c> e <c>InconsistenciaNegocio</c>. A mesma execução não conta duas vezes:
    /// devolve <see langword="false"/> sem alterar nada quando já foi contabilizada.
    /// </summary>
    public bool ContabilizarExecucao(ContribuicaoExecucao contribuicao, DateTimeOffset agora, LimitesContextoUsuario limites)
    {
        ArgumentNullException.ThrowIfNull(contribuicao);
        ArgumentNullException.ThrowIfNull(limites);
        if (JaContabilizou(contribuicao.ExecucaoId))
        {
            return false;
        }

        var origem = new OrigemContexto(TipoOrigemContexto.Execucao, contribuicao.ExecucaoId, contribuicao.SessaoId, agora);
        foreach (var grupo in CnpjsAprovados(contribuicao.Documentos).GroupBy(c => c))
        {
            var (cnpj, papel) = grupo.Key;
            var indice = _cnpjsAuditados.FindIndex(c => c.Cnpj == cnpj && c.Papel == papel);
            var anterior = indice < 0 ? 0 : _cnpjsAuditados[indice].QuantidadeDocumentos;
            var atualizado = new CnpjAuditado(cnpj, papel, anterior + grupo.Count(), agora, origem);
            if (indice < 0)
            {
                _cnpjsAuditados.Add(atualizado);
            }
            else
            {
                _cnpjsAuditados[indice] = atualizado;
            }
        }

        foreach (var grupo in AchadosContabilizaveis(contribuicao.Achados).GroupBy(a => a))
        {
            var (chave, categoria) = grupo.Key;
            var indice = _achadosRecorrentes.FindIndex(a => a.Chave == chave && a.Categoria == categoria);
            var anterior = indice < 0 ? 0 : _achadosRecorrentes[indice].Frequencia;
            var atualizado = new AchadoRecorrente(chave, categoria, anterior + grupo.Count(), agora, origem);
            if (indice < 0)
            {
                _achadosRecorrentes.Add(atualizado);
            }
            else
            {
                _achadosRecorrentes[indice] = atualizado;
            }
        }

        _cnpjsAuditados = [.. _cnpjsAuditados.OrderByDescending(c => c.UltimaAuditoriaEm).Take(limites.MaxCnpjs)];
        _achadosRecorrentes = [.. _achadosRecorrentes.OrderByDescending(a => a.UltimaOcorrenciaEm).Take(limites.MaxAchados)];
        _execucoesContabilizadas.Add(contribuicao.ExecucaoId);
        if (_execucoesContabilizadas.Count > MaxExecucoesContabilizadas)
        {
            _execucoesContabilizadas.RemoveRange(0, _execucoesContabilizadas.Count - MaxExecucoesContabilizadas);
        }

        Renovar(agora, limites);
        return true;
    }

    /// <summary>Declaração ou correção explícita pelo usuário (FR-055a, item 3).</summary>
    public void DeclararPreferencias(
        NivelDetalhe nivelDetalhe,
        IReadOnlyCollection<string> tributosInteresse,
        DateTimeOffset agora,
        LimitesContextoUsuario limites,
        Guid? sessaoId = null)
    {
        ArgumentNullException.ThrowIfNull(tributosInteresse);
        ArgumentNullException.ThrowIfNull(limites);
        if (!Enum.IsDefined(nivelDetalhe))
        {
            throw new ArgumentOutOfRangeException(nameof(nivelDetalhe), nivelDetalhe, "Nível de detalhe fora da lista.");
        }

        if (tributosInteresse.Any(t => !TributosInteresse.Permitido(t)))
        {
            throw new ArgumentException("Tributo fora da lista fechada.", nameof(tributosInteresse));
        }

        if (tributosInteresse.Distinct(StringComparer.Ordinal).Count() != tributosInteresse.Count)
        {
            throw new ArgumentException("Tributo repetido.", nameof(tributosInteresse));
        }

        Preferencias = new PreferenciasUsuario(
            nivelDetalhe,
            [.. tributosInteresse],
            new OrigemContexto(TipoOrigemContexto.DeclaracaoUsuario, null, sessaoId, agora));
        Renovar(agora, limites);
    }

    /// <summary>Sem <paramref name="papel"/>, remove o CNPJ nos dois papéis. Devolve se algo foi removido.</summary>
    public bool RemoverCnpj(string cnpj, PapelCnpj? papel, DateTimeOffset agora, LimitesContextoUsuario limites)
    {
        ArgumentNullException.ThrowIfNull(limites);
        var removidos = _cnpjsAuditados.RemoveAll(c => c.Cnpj == cnpj && (papel is null || c.Papel == papel));
        if (removidos > 0)
        {
            Renovar(agora, limites);
        }

        return removidos > 0;
    }

    public bool RemoverAchadoRecorrente(string chave, DateTimeOffset agora, LimitesContextoUsuario limites)
    {
        ArgumentNullException.ThrowIfNull(limites);
        var removidos = _achadosRecorrentes.RemoveAll(a => a.Chave == chave);
        if (removidos > 0)
        {
            Renovar(agora, limites);
        }

        return removidos > 0;
    }

    public static bool CnpjValido(string? cnpj) => cnpj is not null && Cnpj().IsMatch(cnpj);

    public static bool ChaveAchadoValida(string? chave) => chave is not null && ChaveAchado().IsMatch(chave);

    private static IEnumerable<(string Cnpj, PapelCnpj Papel)> CnpjsAprovados(IEnumerable<DocumentoAuditado> documentos)
    {
        foreach (var documento in documentos.Where(d => d.AprovadoNoLayout))
        {
            if (CnpjValido(documento.CnpjEmitente))
            {
                yield return (documento.CnpjEmitente!, PapelCnpj.Emitente);
            }

            if (CnpjValido(documento.CnpjDestinatario))
            {
                yield return (documento.CnpjDestinatario!, PapelCnpj.Destinatario);
            }
        }
    }

    private static IEnumerable<(string Chave, CategoriaAchado Categoria)> AchadosContabilizaveis(IEnumerable<Achado> achados)
    {
        foreach (var achado in achados.Where(a => a.Origem == OrigemAchado.Deterministica))
        {
            var chave = achado.Categoria switch
            {
                CategoriaAchado.InconsistenciaNegocio => achado.RegraId,
                CategoriaAchado.ErroEstrutural => ChaveViolacaoSchema,
                _ => null,
            };
            if (ChaveAchadoValida(chave))
            {
                yield return (chave!, achado.Categoria);
            }
        }
    }

    private void Renovar(DateTimeOffset agora, LimitesContextoUsuario limites)
    {
        if (agora > UltimaAtividadeEm)
        {
            UltimaAtividadeEm = agora;
        }

        AtualizadoEm = agora;
        ExpiraEm = UltimaAtividadeEm + limites.RetencaoInatividade;
    }

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Hex();

    [GeneratedRegex(@"\A[0-9A-Z]{12}[0-9]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Cnpj();

    [GeneratedRegex(@"\A[A-Za-z0-9._-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ChaveAchado();
}

/// <summary>Limites de <c>ContextoUsuario:*</c> aplicados às listas e à retenção.</summary>
public sealed record LimitesContextoUsuario(TimeSpan RetencaoInatividade, int MaxCnpjs, int MaxAchados)
{
    public TimeSpan RetencaoInatividade { get; } = RetencaoInatividade > TimeSpan.Zero
        ? RetencaoInatividade
        : throw new ArgumentOutOfRangeException(nameof(RetencaoInatividade));

    public int MaxCnpjs { get; } = MaxCnpjs > 0 ? MaxCnpjs : throw new ArgumentOutOfRangeException(nameof(MaxCnpjs));

    public int MaxAchados { get; } = MaxAchados > 0 ? MaxAchados : throw new ArgumentOutOfRangeException(nameof(MaxAchados));
}

/// <summary>Resultados determinísticos de uma execução concluída que podem entrar no contexto.</summary>
public sealed record ContribuicaoExecucao(
    Guid ExecucaoId,
    Guid SessaoId,
    IReadOnlyList<DocumentoAuditado> Documentos,
    IReadOnlyList<Achado> Achados);

/// <summary>Documento da execução: só os aprovados no layout contam (FR-055a).</summary>
public sealed record DocumentoAuditado(bool AprovadoNoLayout, string? CnpjEmitente, string? CnpjDestinatario);

public enum PapelCnpj
{
    Emitente,
    Destinatario,
}

public enum TipoOrigemContexto
{
    Execucao,
    DeclaracaoUsuario,
}

public enum NivelDetalhe
{
    Resumido,
    Detalhado,
}

/// <summary>De onde veio cada item do contexto (FR-055b).</summary>
public sealed record OrigemContexto(TipoOrigemContexto Tipo, Guid? ExecucaoId, Guid? SessaoId, DateTimeOffset RegistradoEm);

public sealed record CnpjAuditado(string Cnpj, PapelCnpj Papel, int QuantidadeDocumentos, DateTimeOffset UltimaAuditoriaEm, OrigemContexto Origem);

/// <summary><c>Chave</c> é o <c>regraId</c> ou <see cref="ContextoUsuario.ChaveViolacaoSchema"/>.</summary>
public sealed record AchadoRecorrente(string Chave, CategoriaAchado Categoria, int Frequencia, DateTimeOffset UltimaOcorrenciaEm, OrigemContexto Origem);

public sealed record PreferenciasUsuario(NivelDetalhe NivelDetalhe, IReadOnlyList<string> TributosInteresse, OrigemContexto Origem);

/// <summary>Lista fechada de <c>tributosInteresse</c> (contrato <c>TributoInteresse</c>).</summary>
public static class TributosInteresse
{
    public static IReadOnlyList<string> Lista { get; } = ["ICMS", "ICMS-ST", "FCP", "IPI", "PIS", "COFINS", "ISSQN", "IBS", "CBS", "IS"];

    public static bool Permitido(string? tributo) => tributo is not null && Lista.Contains(tributo, StringComparer.Ordinal);
}
