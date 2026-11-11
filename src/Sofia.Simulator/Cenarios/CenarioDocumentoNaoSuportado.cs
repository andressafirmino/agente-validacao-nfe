using Sofia.Simulator.Contrato;
using Sofia.Simulator.Exemplos;
using Sofia.Simulator.Exibicao;

namespace Sofia.Simulator.Cenarios;

/// <summary>TS-065 opcional: NF-e válida + texto que não é NF-e (FR-051).</summary>
public sealed class CenarioDocumentoNaoSuportado : ICenarioSimulador
{
    public string Nome => "documento-nao-suportado";

    public async Task ExecutarAsync(ContextoCenario contexto, CancellationToken cancellationToken)
    {
        var idExecucao = Guid.CreateVersion7();
        var idSolicitacao = Guid.CreateVersion7();
        var solicitacao = new SolicitacaoAnaliseSofia(
            ContratoSofiaV1.Versao,
            idExecucao,
            idSolicitacao,
            new UsuarioSofia("Integradora Fictícia"),
            "Analise todos os documentos, inclusive o anexo inválido.",
            [
                new DocumentoSofia("doc-nfe", "nfe-valida.xml", RepositorioExemplos.Ler("nfe-valida.xml")),
                new DocumentoSofia("doc-lixo", "texto-nao-nfe.txt", RepositorioExemplos.Ler("texto-nao-nfe.txt")),
            ]);

        var (tarefa, resultado) = await EnvioSofia.EnviarAsync(contexto.ClienteA2A, solicitacao, contexto.Saida, cancellationToken);
        ExibicaoConsole.ExibirTarefa(contexto.Saida, tarefa, resultado, idExecucao, idSolicitacao);
    }
}
