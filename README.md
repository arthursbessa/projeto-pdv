# PDV Desktop (WPF + SQLite local)

Aplicação de PDV WPF operando **100% local/offline**, sem chamadas HTTP. Toda persistência é feita em SQLite.

## O que está implementado

- Banco local único em `./data/pdv-local.db`.
- Criação automática de pasta, arquivo e schema no primeiro run.
- Tabelas locais: `products`, `sales`, `sale_items`, `outbox_events`.
- Finalização de venda salva `sales` + `sale_items` + `outbox_events` na **mesma transação**.
- Seed automático com 20 produtos fake quando `products` está vazia.
- Tela de **Cadastro de Produtos** (listar, buscar, adicionar, editar, ativar/desativar).
- UI do PDV com layout moderno e foco em teclado.

## Estrutura

- `src/Pdv.Ui`: interface WPF (PDV + cadastro de produtos).
- `src/Pdv.Application`: domínio e regras de aplicação.
- `src/Pdv.Infrastructure`: SQLite, schema, seed e repositórios.
- `tests/Pdv.Tests`: testes unitários.

## Executar

```bash
dotnet restore
dotnet build PdvDesktop.sln
dotnet run --project src/Pdv.Ui/Pdv.Ui.csproj
```

## Atalhos no PDV

- `ENTER`: adicionar item por código de barras.
- `F2`: finalizar venda.
- `F4`: remover item selecionado.
- `ESC`: cancelar venda atual.
