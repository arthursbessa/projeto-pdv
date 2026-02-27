# PDV Desktop (WPF + SQLite + Outbox)

MVP de um PDV desktop offline-first em .NET 8 com sincronização confiável de vendas para API web existente.

## Estrutura da solução

- `src/Pdv.Ui`: aplicação WPF (entrada de código de barras, grid de itens, finalização, atalhos de teclado).
- `src/Pdv.Application`: regras de domínio e serviços de aplicação.
- `src/Pdv.Infrastructure`: SQLite, repositórios, clientes HTTP e setup.
- `tests/Pdv.Tests`: testes unitários de regra de adição de item e política de backoff.

## Pré-requisitos

- .NET SDK 8.0+
- Windows (para executar o projeto WPF)

## Como rodar

```bash
dotnet restore
dotnet build PdvDesktop.sln
dotnet run --project src/Pdv.Ui/Pdv.Ui.csproj
```

No primeiro run o arquivo SQLite local (`pdv-local.db`) é criado automaticamente com o schema necessário.

## Configuração

Edite `src/Pdv.Ui/appsettings.json`:

```json
{
  "Pdv": {
    "ApiBaseUrl": "https://seu-backend.com",
    "ApiToken": "",
    "SyncIntervalSeconds": 30
  }
}
```

- `ApiBaseUrl`: URL base da API web.
- `ApiToken`: opcional.
- `SyncIntervalSeconds`: intervalo do sincronizador em segundos.

## Fluxo offline-first

1. Venda é registrada localmente em `sales` + `sale_items`.
2. Na mesma transação, um evento `SaleCreated` é inserido em `outbox_events`.
3. `SyncService` roda periodicamente e no botão **Sincronizar agora**.
4. Eventos pendentes são enviados para `POST /api/pdv/sales`.
5. Em erro, aplica backoff exponencial: 10s, 30s, 2m, 5m, 15m, 30m (máx).

## Atalhos de teclado

- `F2`: finalizar venda
- `F4`: remover item selecionado
- `ESC`: cancelar venda

## Testes

```bash
dotnet test tests/Pdv.Tests/Pdv.Tests.csproj
```
