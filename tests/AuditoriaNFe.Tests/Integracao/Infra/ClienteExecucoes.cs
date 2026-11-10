using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.Agente.Identificacao;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Fixtures.Xml;

namespace AuditoriaNFe.Tests.Integracao.Infra;

/// <summary>Arquivo enviado em <c>arquivos[]</c>.</summary>
public sealed record ArquivoEnvio(string Nome, byte[] Conteudo)
{
    /// <summary>Fixture de T044 pelo caminho relativo, com o nome do arquivo.</summary>
    public static ArquivoEnvio Fixture(string caminho) =>
        new(Path.GetFileName(caminho), FixturesXml.Todas().Single(f => f.Caminho == caminho).Conteudo);
}

/// <summary>Resposta HTTP do canal direto, com o corpo lido.</summary>
public sealed record RespostaExecucao(HttpStatusCode Status, string Corpo, HttpResponseHeaders Cabecalhos)
{
    public EnvelopeV1 Envelope => JsonSerializer.Deserialize<EnvelopeV1>(Corpo, OpcoesJsonContrato.Padrao)
        ?? throw new InvalidOperationException("Corpo sem envelope.");

    public ErroRespostaV1 ErroResposta => JsonSerializer.Deserialize<ErroRespostaV1>(Corpo, OpcoesJsonContrato.Padrao)
        ?? throw new InvalidOperationException("Corpo sem ErroResposta.");
}

/// <summary>Chamadas a <c>/api/v1/execucoes</c> como no contrato OpenAPI.</summary>
public static class ClienteExecucoes
{
    public const string Rota = "/api/v1/execucoes";
    public const string UsuarioPadrao = "usuario-teste";
    public const string ChamadorPadrao = "teste-integracao";

    public static async Task<RespostaExecucao> CriarAsync(
        HttpClient cliente,
        IEnumerable<ArquivoEnvio>? arquivos = null,
        string? pergunta = null,
        string? usuarioId = UsuarioPadrao,
        string? chamadorId = ChamadorPadrao,
        Guid? sessaoId = null,
        string? correlacaoId = null,
        CancellationToken cancelamento = default)
    {
        using var formulario = new MultipartFormDataContent();
        if (pergunta is not null)
        {
            formulario.Add(new StringContent(pergunta), "pergunta");
        }

        foreach (var arquivo in arquivos ?? [])
        {
            var conteudo = new ByteArrayContent(arquivo.Conteudo);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            formulario.Add(conteudo, "arquivos", arquivo.Nome);
        }

        if (sessaoId is not null)
        {
            formulario.Add(new StringContent(sessaoId.Value.ToString()), "sessaoId");
        }

        if (correlacaoId is not null)
        {
            formulario.Add(new StringContent(correlacaoId), "correlacaoId");
        }

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Rota) { Content = formulario };
        Identificar(requisicao, usuarioId, chamadorId);
        return await EnviarAsync(cliente, requisicao, cancelamento);
    }

    public static async Task<RespostaExecucao> ObterAsync(
        HttpClient cliente,
        Guid execucaoId,
        string? usuarioId = UsuarioPadrao,
        string? chamadorId = ChamadorPadrao,
        CancellationToken cancelamento = default)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"{Rota}/{execucaoId}");
        Identificar(requisicao, usuarioId, chamadorId);
        return await EnviarAsync(cliente, requisicao, cancelamento);
    }

    public static void Identificar(HttpRequestMessage requisicao, string? usuarioId, string? chamadorId)
    {
        if (usuarioId is not null)
        {
            requisicao.Headers.Add(LeitorIdentificacaoHttp.CabecalhoUsuario, usuarioId);
        }

        if (chamadorId is not null)
        {
            requisicao.Headers.Add(LeitorIdentificacaoHttp.CabecalhoChamador, chamadorId);
        }
    }

    public static async Task<RespostaExecucao> EnviarAsync(HttpClient cliente, HttpRequestMessage requisicao, CancellationToken cancelamento)
    {
        using var resposta = await cliente.SendAsync(requisicao, cancelamento);
        return new RespostaExecucao(resposta.StatusCode, await resposta.Content.ReadAsStringAsync(cancelamento), resposta.Headers);
    }
}
