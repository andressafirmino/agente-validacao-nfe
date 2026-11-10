using AuditoriaNFe.Domain.ContextoUsuario;

namespace AuditoriaNFe.Application.Contratos;

/// <summary>Schema <c>ContextoUsuario</c> do contrato OpenAPI (FR-059).</summary>
public sealed record ContextoUsuarioV1(
    string UsuarioId,
    string ChamadorId,
    IReadOnlyList<CnpjAuditadoV1> CnpjsAuditados,
    IReadOnlyList<AchadoRecorrenteV1> AchadosRecorrentes,
    PreferenciasV1? Preferencias,
    DateTimeOffset UltimaAtividadeEm,
    DateTimeOffset ExpiraEm)
{
    public static ContextoUsuarioV1 De(ContextoUsuario contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return new ContextoUsuarioV1(
            contexto.UsuarioId,
            contexto.ChamadorId,
            [.. contexto.CnpjsAuditados.Select(c => new CnpjAuditadoV1(c.Cnpj, c.Papel.ToString(), c.QuantidadeDocumentos, c.UltimaAuditoriaEm, OrigemV1.De(c.Origem)))],
            [.. contexto.AchadosRecorrentes.Select(a => new AchadoRecorrenteV1(a.Chave, a.Categoria.ToString(), a.Frequencia, a.UltimaOcorrenciaEm, OrigemV1.De(a.Origem)))],
            contexto.Preferencias is { } preferencias
                ? new PreferenciasV1(preferencias.NivelDetalhe.ToString(), [.. preferencias.TributosInteresse], OrigemV1.De(preferencias.Origem))
                : null,
            contexto.UltimaAtividadeEm,
            contexto.ExpiraEm);
    }
}

public sealed record CnpjAuditadoV1(string Cnpj, string Papel, int QuantidadeDocumentos, DateTimeOffset UltimaAuditoriaEm, OrigemV1 Origem);

public sealed record AchadoRecorrenteV1(string Chave, string Categoria, int Frequencia, DateTimeOffset UltimaOcorrenciaEm, OrigemV1 Origem);

public sealed record PreferenciasV1(string NivelDetalhe, IReadOnlyList<string> TributosInteresse, OrigemV1 Origem);

public sealed record OrigemV1(string Tipo, Guid? ExecucaoId, Guid? SessaoId, DateTimeOffset RegistradoEm)
{
    public static OrigemV1 De(OrigemContexto origem)
    {
        ArgumentNullException.ThrowIfNull(origem);
        return new OrigemV1(origem.Tipo.ToString(), origem.ExecucaoId, origem.SessaoId, origem.RegistradoEm);
    }
}

/// <summary>Schema <c>PreferenciasEntrada</c>; a leitura confere a lista fechada e campos extras.</summary>
public sealed record PreferenciasEntradaV1(string NivelDetalhe, IReadOnlyList<string> TributosInteresse);
