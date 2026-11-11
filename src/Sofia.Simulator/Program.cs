using System.Net.Http.Headers;
using Sofia.Simulator;

var opcoes = OpcoesLinhaComando.Ler(args);
if (opcoes.Erro is { } mensagemErro)
{
    await Console.Error.WriteLineAsync(mensagemErro);
    await Console.Error.WriteLineAsync("Uso: dotnet run --project src/Sofia.Simulator -- --endereco <url> --cenario <basico|documento-nao-suportado|contrato-invalido>");
    return 2;
}

var credencial = Environment.GetEnvironmentVariable("SOFIA_A2A_TOKEN");
if (string.IsNullOrWhiteSpace(credencial))
{
    await Console.Error.WriteLineAsync(
        "Defina a variável de ambiente SOFIA_A2A_TOKEN com a credencial de serviço da SOFIA (mesmo valor do agente).");
    return 2;
}

using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credencial.Trim());

return await ExecutorSimulador.ExecutarAsync(
    opcoes.Endereco,
    opcoes.Cenario,
    http,
    Console.Out,
    Console.Error);

internal static class OpcoesLinhaComando
{
    internal static (Uri Endereco, string Cenario, string? Erro) Ler(string[] argumentos)
    {
        var endereco = new Uri("http://localhost:5310");
        var cenario = "basico";

        for (var i = 0; i < argumentos.Length; i++)
        {
            var arg = argumentos[i];
            if (arg is "--endereco" or "-e")
            {
                if (++i >= argumentos.Length)
                {
                    return (endereco, cenario, "Falta o valor de --endereco.");
                }

                if (!Uri.TryCreate(argumentos[i], UriKind.Absolute, out var uri))
                {
                    return (endereco, cenario, "Endereço inválido em --endereco.");
                }

                endereco = uri;
                continue;
            }

            if (arg is "--cenario" or "-c")
            {
                if (++i >= argumentos.Length)
                {
                    return (endereco, cenario, "Falta o valor de --cenario.");
                }

                cenario = argumentos[i];
            }
        }

        return (endereco, cenario, null);
    }
}
