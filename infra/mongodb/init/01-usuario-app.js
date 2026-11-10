// Executado pelo entrypoint do MongoDB só na primeira inicialização (volume vazio), com o usuário root.
// Só no contêiner local: no Atlas, o usuário é criado pelo painel (quickstart §8.9).

const nomeBanco = "auditoria_agente";
const usuarioApp = process.env.MONGO_APP_USERNAME;
const senhaApp = process.env.MONGO_APP_PASSWORD;

if (!usuarioApp || !senhaApp) {
  throw new Error("Defina MONGO_APP_USERNAME e MONGO_APP_PASSWORD no arquivo .env");
}

// Usuário do Api.Agente: leitura e escrita só neste banco.
db.getSiblingDB(nomeBanco).createUser({
  user: usuarioApp,
  pwd: senhaApp,
  roles: [{ role: "readWrite", db: nomeBanco }]
});
