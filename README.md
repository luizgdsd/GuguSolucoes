# GuguSolucoes (Projeto Unificado)

Aplicativo desktop unificado para rotinas de TI no escritorio de contabilidade, juntando:

- modulo VoltaGOV (diagnostico/reparo de conectividade Gov.br),
- modulo LimpaCache (limpeza de temporarios manual e automatica).

## Base tecnica

- C# WinForms em .NET 8 (`WinExe`)
- instancia unica da interface via `Mutex` em `Program.cs`
- modo agente de limpeza no mesmo executavel via `--agent`
- icone do app e instalador: `favicon.ico`
- auto-update via GitHub Releases (`luizgdsd/GuguSolucoes`) com manifesto `update.json`

## Funcionalidades preservadas

- app residente na tray
- botao "Executar Reparo Agora" para DNS/TCP 443 em dominios Gov.br
- reparo local: fechar navegadores, `ipconfig /flushdns`, limpeza de cache de navegadores
- aba LimpaCache com:
  - configuracao de intervalo/alvos
  - execucao manual
  - status da ultima execucao
  - abertura da pasta de logs
- agente de limpeza em background com tarefa agendada SYSTEM + Highest

## Persistencia em runtime

- Config/logs do app unificado: `%LOCALAPPDATA%\GuguSolucoes`
- Config/estado/logs do LimpaCache: `C:\ProgramData\LimpaCache`

## Build

```powershell
cd "E:\Gugu Soluções\GuguSolucoes.Unificado"
dotnet build src\GuguSolucoes.Desktop\GuguSolucoes.Desktop.csproj -c Release
```

## Instalador

```powershell
cd "E:\Gugu Soluções\GuguSolucoes.Unificado"
.\build-installer.ps1 -AppVersion "1.0.0"
```

Saidas:

- `dist\publish\GuguSolucoes.exe`
- `dist\GuguSolucoes-Setup-1.0.0.exe`

## Release / update

O app consulta a ultima GitHub Release do repositorio configurado em `settings.json`.
Cada release deve conter:

- `GuguSolucoes-Setup-x.y.z.exe`
- `update.json`

Para publicar uma nova versao:

```powershell
.\scripts\publish-update.ps1 -Version "1.0.23"
```

O push da tag `v1.0.23` aciona `.github/workflows/release.yml`, gera o instalador,
calcula o SHA-256, cria o `update.json` e publica a release.
