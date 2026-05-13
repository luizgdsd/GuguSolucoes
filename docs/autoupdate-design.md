# Projeto de Auto Update

Este documento define o desenho recomendado para o auto update do GuguSolucoes.
O objetivo principal e instalar a versao nova removendo os binarios e dependencias
da versao antiga, mantendo somente os arquivos necessarios para a versao atual.

## Fonte das versoes

- Publicar cada versao como uma GitHub Release.
- Anexar o instalador `GuguSolucoes-Setup-x.y.z.exe`.
- Anexar tambem um manifesto `update.json` com:
  - `version`
  - `installerUrl`
  - `sha256`
  - `mandatory`
  - `releaseNotes`
  - `publishedAtUtc`
- Assinar o instalador quando houver certificado disponivel.

Exemplo de manifesto:

```json
{
  "version": "1.0.22",
  "installerUrl": "https://github.com/gugu-solucoes/GuguSolucoes/releases/download/v1.0.22/GuguSolucoes-Setup-1.0.22.exe",
  "sha256": "HASH_DO_ARQUIVO",
  "mandatory": false,
  "releaseNotes": "Troca do icone e suporte ao Vivaldi.",
  "publishedAtUtc": "2026-05-13T00:00:00Z"
}
```

## Fluxo no aplicativo

1. O app verifica atualizacoes somente se `EnableAutoUpdate` estiver ativo.
2. O app consulta o manifesto ou a ultima GitHub Release configurada em `GitHubRepo`.
3. Se `version` for maior que a versao instalada, o app baixa o instalador para
   `%LOCALAPPDATA%\GuguSolucoes\updates\<version>`.
4. O app valida o SHA-256 do arquivo baixado.
5. Se a validacao passar, o app inicia um processo externo de update e encerra a UI.
6. O processo externo executa o instalador em modo silencioso.
7. Depois do instalador terminar, a nova versao e iniciada.

## Limpeza da versao anterior

O instalador deve ser a fronteira de limpeza. Ele ja foi configurado para:

- fechar `GuguSolucoes.exe` antes da instalacao;
- impedir dois instaladores simultaneos com `SetupMutex`;
- apagar `{app}\*` antes de copiar os novos arquivos;
- copiar somente o conteudo atual de `dist\publish`;
- recriar a tarefa agendada do LimpaCache quando selecionada.

Como configuracoes e logs ficam fora da pasta do app, a limpeza de `{app}` remove
apenas binarios, DLLs e dependencias antigas. Dados de runtime permanecem em:

- `%LOCALAPPDATA%\GuguSolucoes`
- `C:\ProgramData\LimpaCache`

## Processo externo recomendado

Criar no futuro um executavel pequeno, por exemplo `GuguSolucoes.Updater.exe`,
ou um modo CLI no proprio app:

```powershell
GuguSolucoes.exe --apply-update "<caminho-do-instalador>"
```

Responsabilidades:

- aguardar a UI sair;
- parar a tarefa `GuguSolucoes TempCleanup Agent`;
- executar o setup com `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`;
- registrar log em `%LOCALAPPDATA%\GuguSolucoes\logs\update-yyyyMMdd.log`;
- reiniciar o app ao final;
- em falha, manter o instalador baixado e registrar o erro sem remover dados.

## Politica de rollback

O rollback deve ser manual na primeira versao do auto update:

- manter no GitHub Releases o instalador da versao anterior;
- permitir instalar uma versao anterior por cima;
- como o instalador limpa `{app}` antes de copiar arquivos, o downgrade tambem
  nao deixa DLLs misturadas.

## Cuidados de seguranca

- Nunca executar arquivo baixado sem validar SHA-256.
- Preferir instalador assinado.
- Nao gravar `GitHubToken` em texto puro se releases privadas forem usadas.
- Baixar somente de HTTPS.
- Registrar logs de update sem expor token ou URL assinada temporaria.
