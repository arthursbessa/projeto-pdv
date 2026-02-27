# PDV Desktop (WPF + SQLite local)

Aplicação de PDV WPF operando **100% local/offline**, sem APIs de hardware específicas para leitor: o scanner funciona como teclado USB (HID keyboard).

## O que está implementado

- Fluxo completo de venda local:
  - leitura por scanner HID (código + ENTER) ou digitação manual;
  - adiciona item por barcode;
  - incrementa quantidade quando o produto já está na lista;
  - permite editar quantidade no grid com validação;
  - remove item selecionado;
  - finaliza venda com modal de pagamento e gravação transacional em SQLite (`sales` + `sale_items` + `outbox_events`);
  - limpa venda após finalizar/cancelar e retorna foco para o campo de código.
- Feedback de operação na barra de status (sucesso/erro/validação).
- Banco local único com criação automática de pasta, arquivo, schema e índices no primeiro run.
- Seed automático com 20 produtos de exemplo (barcode numérico de 13 dígitos) quando `products` está vazia.
- Tela de **Cadastro de Produtos** (listar, buscar, adicionar, editar, ativar/desativar).

## Estrutura

- `src/Pdv.Ui`: interface WPF (PDV + cadastro de produtos).
- `src/Pdv.Application`: domínio e regras de aplicação.
- `src/Pdv.Infrastructure`: SQLite, schema, seed e repositórios.
- `tests/Pdv.Tests`: testes unitários.

## Como rodar

```bash
dotnet restore
dotnet build PdvDesktop.sln
dotnet test
dotnet run --project src/Pdv.Ui/Pdv.Ui.csproj
```

## Como registrar uma venda (passo a passo)

1. Abra o PDV.
2. Clique no campo **Código de barras** (ou use `CTRL+F`).
3. Escaneie o produto no leitor HID **ou** digite o código manualmente.
4. Pressione `ENTER` (no teclado ou no ENTER enviado pelo scanner) para adicionar.
5. Repita para os demais itens.
6. Se quiser ajustar, edite a coluna **Qtd** diretamente no grid.
7. Se necessário, selecione um item e pressione `F4` para remover.
8. Pressione `F2` para abrir a modal de finalização e escolha o pagamento.
9. Confirme: a venda é persistida localmente e a tela é limpa para a próxima venda.

## Atalhos implementados

- `ENTER` no campo de barcode: adicionar item.
- `F2`: abrir modal de finalização (pagamento).
- `F4`: remover item selecionado.
- `ESC`: cancelar venda atual (limpa itens e total).
- `CTRL+F`: focar o campo de barcode a qualquer momento.

## Como testar com leitor HID (scanner USB)

1. Conecte o scanner USB configurado em modo teclado (HID).
2. No PDV, clique no campo de código (ou use `CTRL+F`).
3. Escaneie o código de barras.
4. O scanner normalmente envia o código e `ENTER`; isso deve adicionar o item automaticamente.

## SQLite local (caminho exato)

**O banco é criado em: `./data/pdv-local.db` (relativo ao diretório de execução do app).**

### Como localizar no Windows

- Se você rodar a partir da pasta do projeto com `dotnet run`, o arquivo pode aparecer dentro do diretório de saída (`bin/...`) usado pela execução.
- Em builds/publicações, localize o arquivo na pasta onde o executável está sendo executado, dentro de `data\pdv-local.db`.
