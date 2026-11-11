using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Api.RAG.Infraestrutura.Migracoes
{
    /// <inheritdoc />
    public partial class InicialRag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rag");

            migrationBuilder.CreateTable(
                name: "documentos",
                schema: "rag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id_externo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    titulo = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    modelo = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    versao_layout = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    versao_documento = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    hash_conteudo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fonte = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ingestido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responsavel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "jobs_ingestao",
                schema: "rag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    documento_id_externo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    versao_documento = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    hash_conteudo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mensagem_erro = table.Column<string>(type: "text", nullable: true),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs_ingestao", x => x.id);
                    table.CheckConstraint("ck_jobs_ingestao_estado", "estado IN ('Pendente', 'Processando', 'Concluido', 'Falhou')");
                });

            migrationBuilder.CreateTable(
                name: "chunks",
                schema: "rag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<int>(type: "integer", nullable: false),
                    trecho = table.Column<string>(type: "text", nullable: false),
                    secao = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    pagina = table.Column<int>(type: "integer", nullable: true),
                    regras_relacionadas = table.Column<string[]>(type: "text[]", nullable: false),
                    palavras_chave = table.Column<string[]>(type: "text[]", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_chunks_documentos",
                        column: x => x.documento_id,
                        principalSchema: "rag",
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chamadas_embedding",
                schema: "rag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_ingestao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modelo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tokens_entrada = table.Column<int>(type: "integer", nullable: false),
                    custo_estimado_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chamadas_embedding", x => x.id);
                    table.CheckConstraint("ck_chamadas_embedding_tokens", "tokens_entrada >= 0");
                    table.ForeignKey(
                        name: "fk_chamadas_embedding_jobs",
                        column: x => x.job_ingestao_id,
                        principalSchema: "rag",
                        principalTable: "jobs_ingestao",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_chamadas_embedding_job_ingestao_id",
                schema: "rag",
                table: "chamadas_embedding",
                column: "job_ingestao_id");

            migrationBuilder.CreateIndex(
                name: "ix_chunks_documento_id",
                schema: "rag",
                table: "chunks",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ux_chunks_chunk_id",
                schema: "rag",
                table: "chunks",
                column: "chunk_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_documentos_externo_versao_hash",
                schema: "rag",
                table: "documentos",
                columns: new[] { "documento_id_externo", "versao_documento", "hash_conteudo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chamadas_embedding",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "chunks",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "jobs_ingestao",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "documentos",
                schema: "rag");
        }
    }
}
