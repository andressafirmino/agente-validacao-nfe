namespace Sofia.Simulator.Exemplos;

/// <summary>XMLs sintéticos copiados para a saída do projeto (FR-050).</summary>
public static class RepositorioExemplos
{
    private static readonly string Pasta = Path.Combine(
        Path.GetDirectoryName(typeof(RepositorioExemplos).Assembly.Location) ?? AppContext.BaseDirectory,
        "Exemplos");

    public static string Ler(string nomeArquivo)
    {
        var caminho = Path.Combine(Pasta, nomeArquivo);
        if (!File.Exists(caminho))
        {
            throw new FileNotFoundException($"Exemplo não encontrado: {caminho}");
        }

        return File.ReadAllText(caminho);
    }
}
