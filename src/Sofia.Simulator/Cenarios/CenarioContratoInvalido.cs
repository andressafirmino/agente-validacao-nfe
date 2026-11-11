using Sofia.Simulator.Contrato;
using Sofia.Simulator.Exemplos;
using Sofia.Simulator.Exibicao;

namespace Sofia.Simulator.Cenarios;

/// <summary>TS-066 opcional: versão de contrato não suportada (FR-051).</summary>
public sealed class CenarioContratoInvalido : ICenarioSimulador
{
    public string Nome => "contrato-invalido";

    public async Task ExecutarAsync(ContextoCenario contexto, CancellationToken cancellationToken)
    {
        var idExecucao = Guid.CreateVersion7();
        var idSolicitacao = Guid.CreateVersion7();
        var solicitacao = new SolicitacaoAnaliseSofia(
            "2.0",
            idExecucao,
            idSolicitacao,
            new UsuarioSofia("Integradora Fictícia"),
            "Esta solicitação usa uma versão de contrato inválida.",
            [
                new DocumentoSofia("doc-nfe", "nfe-valida.xml", RepositorioExemplos.Ler("nfe-valida.xml")),
            ]);

        var (tarefa, resultado) = await EnvioSofia.EnviarAsync(contexto.ClienteA2A, solicitacao, contexto.Saida, cancellationToken);
        ExibicaoConsole.ExibirTarefa(contexto.Saida, tarefa, resultado, idExecucao, idSolicitacao);
    }
}
