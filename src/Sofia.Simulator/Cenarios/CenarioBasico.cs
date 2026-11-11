using Sofia.Simulator.Contrato;
using Sofia.Simulator.Exemplos;
using Sofia.Simulator.Exibicao;

namespace Sofia.Simulator.Cenarios;

/// <summary>TS-035: descoberta, envio e exibição do resultado (FR-049).</summary>
public sealed class CenarioBasico : ICenarioSimulador
{
    public string Nome => "basico";

    public async Task ExecutarAsync(ContextoCenario contexto, CancellationToken cancellationToken)
    {
        var idExecucao = Guid.CreateVersion7();
        var idSolicitacao = Guid.CreateVersion7();
        var solicitacao = new SolicitacaoAnaliseSofia(
            ContratoSofiaV1.Versao,
            idExecucao,
            idSolicitacao,
            new UsuarioSofia("Integradora Fictícia"),
            "Verifique divergências fiscais nos documentos enviados.",
            [
                new DocumentoSofia("doc-nfe", "nfe-totalizador-divergente.xml", RepositorioExemplos.Ler("nfe-totalizador-divergente.xml")),
            ]);

        var (tarefa, resultado) = await EnvioSofia.EnviarAsync(contexto.ClienteA2A, solicitacao, contexto.Saida, cancellationToken);
        ExibicaoConsole.ExibirTarefa(contexto.Saida, tarefa, resultado, idExecucao, idSolicitacao);
    }
}
