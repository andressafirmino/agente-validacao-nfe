using AuditoriaNFe.Application.Abstracoes;

namespace AuditoriaNFe.Infrastructure.Comum;

/// <summary>
/// UUID v7 crescente no processo: ids gerados no mesmo milissegundo seguem a ordem de geração, de
/// modo que ordenar por <c>(instante, id)</c> reproduz a sequência das chamadas.
/// </summary>
public sealed class GeradorIdsV7(IRelogio relogio) : IGeradorIds
{
    private readonly Lock _trava = new();
    private Guid _ultimo = Guid.Empty;

    public Guid Novo()
    {
        var candidato = Guid.CreateVersion7(relogio.Agora);
        lock (_trava)
        {
            _ultimo = candidato.CompareTo(_ultimo) > 0 ? candidato : Incrementar(_ultimo);
            return _ultimo;
        }
    }

    private static Guid Incrementar(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);
        for (var i = 15; i >= 9; i--)
        {
            if (++bytes[i] != 0)
            {
                break;
            }
        }

        return new Guid(bytes, bigEndian: true);
    }
}
