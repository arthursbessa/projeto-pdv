# PDV Desktop (WPF + SQLite local)

AplicaÃ§Ã£o de PDV WPF com cache local SQLite e integraÃ§Ã£o com o backend Lovable/Supabase para autenticaÃ§Ã£o, catÃ¡logo e envio de vendas. O scanner funciona como teclado USB (HID keyboard).

## O que estÃ¡ implementado

- Fluxo completo de venda local:
  - leitura por scanner HID (cÃ³digo + ENTER) ou digitaÃ§Ã£o manual;
  - adiciona item por barcode;
  - incrementa quantidade quando o produto jÃ¡ estÃ¡ na lista;
  - permite editar quantidade no grid com validaÃ§Ã£o;
  - remove item selecionado;
  - finaliza venda com modal de pagamento e gravaÃ§Ã£o transacional em SQLite (`sales` + `sale_items` + `outbox_events`);
  - limpa venda apÃ³s finalizar/cancelar e retorna foco para o campo de cÃ³digo.
- Feedback de operaÃ§Ã£o na barra de status (sucesso/erro/validaÃ§Ã£o).
- Banco local Ãºnico com criaÃ§Ã£o automÃ¡tica de pasta, arquivo, schema e Ã­ndices no primeiro run.
- SincronizaÃ§Ã£o do catÃ¡logo remoto via endpoint `GET /pdv-catalog` com token `x-pdv-token`, armazenando cache local para operaÃ§Ã£o do checkout.
- Envio de vendas para o backend via outbox local e endpoint `POST /pdv-sales` (incluindo `session_id` do caixa aberto).
- Abertura/fechamento de caixa integrado ao backend via endpoint `POST /pdv-cash-register`.
- Sangria integrada ao backend via endpoint `POST /pdv-sangria`.
- Login usando as mesmas credenciais do Lovable (Supabase Auth).
- Login com fechamento automÃ¡tico de caixa aberto em data anterior.
- Bloqueio de abertura do PDV quando nÃ£o houver caixa aberto.
- BotÃ£o **Integrar dados** no menu para envio manual das vendas pendentes (outbox).
- Indicadores visuais de loading nas rotinas de login, sincronizaÃ§Ã£o e finalizaÃ§Ã£o de venda.

## Estrutura

- `src/Pdv.Ui`: interface WPF (menu, login Lovable e PDV).
- `src/Pdv.Application`: domÃ­nio e regras de aplicaÃ§Ã£o.
- `src/Pdv.Infrastructure`: SQLite, schema, seed e repositÃ³rios.
- `tests/Pdv.Tests`: testes unitÃ¡rios.


## ConfiguraÃ§Ã£o de integraÃ§Ã£o (appsettings)

Arquivo: `src/Pdv.Ui/appsettings.json`

- `Pdv:FunctionsBaseUrl`: URL base das Edge Functions do Supabase (ex: `https://<project>.supabase.co/functions/v1`).
- `Pdv:TerminalToken`: token do terminal PDV (`x-pdv-token`).
- `Pdv:SupabaseBaseUrl`: URL base do projeto Supabase para autenticaÃ§Ã£o.
- `Pdv:SupabaseAnonKey`: chave `anon` do Supabase (necessÃ¡ria para login).

> O PDV nÃ£o possui mais telas de cadastro de usuÃ¡rios e produtos. Esses cadastros e gestÃ£o ficam no painel Lovable.


## Logs de erro (arquivo .txt)

- Em caso de falha no login remoto ou na sincronizaÃ§Ã£o inicial apÃ³s login, o PDV grava detalhes no arquivo `logs/errors-AAAAmmdd.txt`.
- O diretÃ³rio `logs` Ã© criado automaticamente no diretÃ³rio de execuÃ§Ã£o da aplicaÃ§Ã£o.
- Apenas erros sÃ£o registrados.

## Como rodar

```bash
dotnet restore
dotnet build PdvDesktop.sln
dotnet test
dotnet run --project src/Pdv.Ui/Pdv.Ui.csproj
```

## Como gerar uma release para o cliente

O projeto possui dois scripts distintos:

- `scripts/release-client.ps1`: gera o pacote da versao e publica a release no GitHub.
- `scripts/install-client-latest.ps1`: usado na implantacao inicial do cliente, baixando a ultima release publicada e instalando no Windows.
- `scripts/release-client.cmd` e `scripts/install-client-latest.cmd`: atalhos para execucao por clique duplo, sem precisar digitar o comando completo no terminal.

### Publicar uma nova release

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-client.ps1 -Version v1.1.0
```

Tambem e possivel executar [release-client.cmd](/c:/Projetos/projeto-pdv/scripts/release-client.cmd) com duplo clique. Ele chama o PowerShell automaticamente.

Se preferir um fluxo ainda mais simples, voce pode preencher os valores padrao no topo de [release-client.ps1](/c:/Projetos/projeto-pdv/scripts/release-client.ps1), principalmente `DefaultVersion`. Se a versao nao estiver preenchida, o script pergunta durante a execucao.

O script de release pode:

- rodar os testes;
- gerar o pacote do cliente fora do repositorio, por padrao em `Documentos\PDV-Client\PDV-Client`;
- criar o arquivo `.zip` da versao;
- embutir a versao da release no executavel publicado;
- criar e enviar a tag para o GitHub;
- criar o Release automaticamente quando `GITHUB_TOKEN` ou `GH_TOKEN` estiver configurado no ambiente.

Sem token configurado, o script ainda gera o pacote e publica a tag, deixando apenas a criacao do Release para ser concluida manualmente no GitHub.

### Implantacao inicial no cliente

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-client-latest.ps1
```

Tambem e possivel executar [install-client-latest.cmd](/c:/Projetos/projeto-pdv/scripts/install-client-latest.cmd) com duplo clique. Ele chama o instalador automaticamente.

No topo de [install-client-latest.ps1](/c:/Projetos/projeto-pdv/scripts/install-client-latest.ps1) voce pode definir os valores padrao de repositorio, nome do pacote e pasta de instalacao. Ao abrir por clique, o script ainda pergunta a pasta de instalacao e permite manter ou trocar o valor.

O script de implantacao:

- consulta a ultima release publicada no GitHub;
- baixa o asset `.zip` mais recente do PDV;
- instala os binarios em `C:\Program Files\PDV-Client`;
- preserva `logs`, `appsettings.json` e `appsettings.local.json` se eles ja existirem;
- cria a pasta `logs` dentro da pasta do sistema.

### Observacoes sobre versao e atualizacao automatica

- Em ambiente de desenvolvimento, o sistema exibe `DEV`.
- Em builds de cliente geradas pelo script de release, o sistema exibe a versao publicada da release, como `v1.1.0`.
- O pacote do cliente e publicado em formato de pasta compactada (`.zip`), o que facilita o fluxo de autoatualizacao.
- O instalador do pacote coloca os arquivos do sistema, por padrao, em `C:\Program Files\PDV-Client`.
- Os logs do sistema ficam em `C:\Program Files\PDV-Client\logs`.
- O banco local e os arquivos de dados ficam em `C:\ProgramData\PDV-Client`.
- O pacote atual fica em uma pasta fixa `PDV-Client`, sem manter historico de releases antigas.
- Quando houver uma nova release no GitHub, o PDV pode detectar isso na abertura e oferecer a atualizacao ao operador.
- A atualizacao automatica preserva `logs`, `appsettings.json` e `appsettings.local.json`, sobrescrevendo os binarios da pasta atual.

## Como registrar uma venda (passo a passo)

1. Abra o PDV.
2. Clique no campo **CÃ³digo de barras** (ou use `CTRL+F`).
3. Escaneie o produto no leitor HID **ou** digite o cÃ³digo manualmente.
4. Pressione `ENTER` (no teclado ou no ENTER enviado pelo scanner) para adicionar.
5. Repita para os demais itens.
6. Se quiser ajustar, edite a coluna **Qtd** diretamente no grid.
7. Se necessÃ¡rio, selecione um item e pressione `F4` para remover.
8. Pressione `F2` para abrir a modal de finalizaÃ§Ã£o e escolha o pagamento.
9. Confirme: a venda Ã© persistida localmente e a tela Ã© limpa para a prÃ³xima venda.

## Atalhos implementados

- `ENTER` no campo de barcode: adicionar item.
- `F2`: abrir modal de finalizaÃ§Ã£o (pagamento).
- `F4`: remover item selecionado.
- `ESC`: cancelar venda atual (limpa itens e total).
- `CTRL+F`: focar o campo de barcode a qualquer momento.

## Como testar com leitor HID (scanner USB)

1. Conecte o scanner USB configurado em modo teclado (HID).
2. No PDV, clique no campo de cÃ³digo (ou use `CTRL+F`).
3. Escaneie o cÃ³digo de barras.
4. O scanner normalmente envia o cÃ³digo e `ENTER`; isso deve adicionar o item automaticamente.

## SQLite local (caminho exato)

**O banco e criado em: `C:\ProgramData\PDV-Client\data\pdv-local.db`.**

### Como localizar no Windows

- Em instalacoes de cliente, os binarios ficam em `C:\Program Files\PDV-Client`.
- Os logs ficam em `C:\Program Files\PDV-Client\logs`.
- Os dados locais ficam em `C:\ProgramData\PDV-Client`.
