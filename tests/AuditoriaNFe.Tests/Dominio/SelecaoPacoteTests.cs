using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Schemas;
using AuditoriaNFe.Tests.Fixtures.Xml;

namespace AuditoriaNFe.Tests.Dominio;

public sealed class SelecaoPacoteTests
{
    private const string Ns = IdentificacaoDocumento.NamespaceNFe;
    private static readonly DateOnly Hoje = new(2026, 11, 20);

    private static readonly IReadOnlyList<VigenciaPacote> Manifesto =
        [.. FixturesXml.Pacotes().Select(p => new VigenciaPacote(p.Ordem, p.Id, p.VigenciaInicio, p.VigenciaFim))];

    private static SeletorPacote Seletor => new(Manifesto);

    [Fact]
    public void Com_protocolo_usa_o_pacote_vigente_no_dhRecbto_TS048()
    {
        var chave = GeradorNFeSintetica.ChaveAcesso(new OpcoesNFeSintetica { DhEmi = new(2026, 8, 2, 23, 58, 0, TimeSpan.FromHours(-3)) });
        var identificacao = IdentificacaoDocumento.Avaliar(
            "nfeProc", Ns, "4.00", "55", "NFe" + chave, true, "2026-08-02T23:58:00-03:00", "2026-08-03T00:03:00-03:00");

        Assert.Equal("PL_010e_v1.02", Seletor.Vigente(identificacao.DataReferencia!.Value, Hoje)!.Id);
    }

    [Fact]
    public void Sem_protocolo_usa_o_pacote_vigente_no_dhEmi_TS049()
    {
        var chave = GeradorNFeSintetica.ChaveAcesso(new OpcoesNFeSintetica { DhEmi = new(2026, 8, 2, 23, 58, 0, TimeSpan.FromHours(-3)) });
        var identificacao = IdentificacaoDocumento.Avaliar(
            "NFe", Ns, "4.00", "55", "NFe" + chave, false, "2026-08-02T23:58:00-03:00", null);

        Assert.Equal("PL_010d_v1.03", Seletor.Vigente(identificacao.DataReferencia!.Value, Hoje)!.Id);
    }

    [Fact]
    public void Data_anterior_ao_primeiro_pacote_nao_tem_pacote_TS051()
    {
        var primeiro = Manifesto[0].Inicio;
        var chamadas = 0;

        var resultado = Seletor.Validar<string>(primeiro.AddDays(-1), Hoje, _ => { chamadas++; return []; });

        Assert.Null(Seletor.Vigente(primeiro.AddDays(-1), Hoje));
        Assert.False(resultado.Encontrado);
        Assert.Null(resultado.PacoteAplicado);
        Assert.Empty(resultado.PacotesTentados);
        Assert.Equal(0, chamadas);
    }

    [Fact]
    public void Data_posterior_a_data_atual_nao_tem_pacote()
    {
        Assert.Null(Seletor.Vigente(Hoje.AddDays(1), Hoje));
        Assert.False(Seletor.Validar<string>(Hoje.AddDays(1), Hoje, _ => []).Encontrado);
    }

    [Fact]
    public void Ultimo_pacote_vale_ate_a_data_atual()
    {
        var ultimo = Manifesto[^1];

        Assert.Equal(ultimo.Id, Seletor.Vigente(Hoje, Hoje)!.Id);
        Assert.Null(Seletor.Vigente(ultimo.Inicio, ultimo.Inicio.AddDays(-1)));
    }

    public static TheoryData<string> Pacotes() => [.. Manifesto.Select(p => p.Id)];

    [Theory]
    [MemberData(nameof(Pacotes))]
    public void Limites_de_inicio_e_fim_de_cada_vigencia(string id)
    {
        var indice = Manifesto.ToList().FindIndex(p => p.Id == id);
        var pacote = Manifesto[indice];

        Assert.Equal(id, Seletor.Vigente(pacote.Inicio, Hoje)!.Id);
        Assert.Equal(id, Seletor.Vigente(pacote.Fim ?? Hoje, Hoje)!.Id);
        Assert.Equal(indice == 0 ? null : Manifesto[indice - 1].Id, Seletor.Vigente(pacote.Inicio.AddDays(-1), Hoje)?.Id);
    }

    [Fact]
    public void Sem_erro_no_vigente_o_anterior_nao_e_tentado()
    {
        var resultado = Seletor.Validar<string>(new DateOnly(2026, 9, 24), Hoje, _ => []);

        Assert.True(resultado.Encontrado);
        Assert.Equal("PL_010e_v1.02", resultado.PacoteVigente!.Id);
        Assert.Equal("PL_010e_v1.02", resultado.PacoteAplicado!.Id);
        Assert.Equal(["PL_010e_v1.02"], resultado.PacotesTentados);
        Assert.False(resultado.EmConvivencia);
        Assert.Empty(resultado.Erros);
    }

    // Tabela sintética: o leiaute 4.00 não tem convivência, mas a regra do FR-010f é genérica.
    private static SeletorPacote SeletorConvivencia => new(
    [
        new VigenciaPacote(1, "PACOTE_A", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), FimConvivencia: new DateOnly(2026, 2, 15)),
        new VigenciaPacote(2, "PACOTE_B", new DateOnly(2026, 2, 1), null),
    ]);

    [Fact]
    public void Layout_do_pacote_anterior_dentro_da_convivencia_aplica_o_anterior_TS060()
    {
        var resultado = SeletorConvivencia.Validar<string>(new DateOnly(2026, 2, 10), Hoje,
            pacote => pacote.Id == "PACOTE_B" ? ["erro no B"] : []);

        Assert.Equal("PACOTE_B", resultado.PacoteVigente!.Id);
        Assert.Equal("PACOTE_A", resultado.PacoteAplicado!.Id);
        Assert.Equal(["PACOTE_B", "PACOTE_A"], resultado.PacotesTentados);
        Assert.True(resultado.EmConvivencia);
        Assert.Empty(resultado.Erros);
    }

    [Fact]
    public void Erro_nos_dois_pacotes_na_convivencia_mantem_os_erros_do_vigente_TS061()
    {
        var resultado = SeletorConvivencia.Validar<string>(new DateOnly(2026, 2, 15), Hoje,
            pacote => [$"erro no {pacote.Id}"]);

        Assert.Equal("PACOTE_B", resultado.PacoteAplicado!.Id);
        Assert.Equal(["PACOTE_B", "PACOTE_A"], resultado.PacotesTentados);
        Assert.True(resultado.EmConvivencia);
        Assert.Equal(["erro no PACOTE_B"], resultado.Erros);
    }

    [Fact]
    public void Apos_o_fim_da_convivencia_o_anterior_nao_e_tentado_TS062()
    {
        var resultado = SeletorConvivencia.Validar<string>(new DateOnly(2026, 2, 16), Hoje,
            pacote => pacote.Id == "PACOTE_B" ? ["erro no B"] : []);

        Assert.Equal("PACOTE_B", resultado.PacoteAplicado!.Id);
        Assert.Equal(["PACOTE_B"], resultado.PacotesTentados);
        Assert.False(resultado.EmConvivencia);
        Assert.Equal(["erro no B"], resultado.Erros);
    }

    [Fact]
    public void Na_convivencia_sem_erro_no_vigente_o_anterior_nao_e_tentado()
    {
        var resultado = SeletorConvivencia.Validar<string>(new DateOnly(2026, 2, 10), Hoje, _ => []);

        Assert.Equal("PACOTE_B", resultado.PacoteAplicado!.Id);
        Assert.Equal(["PACOTE_B"], resultado.PacotesTentados);
        Assert.True(resultado.EmConvivencia);
    }
}
