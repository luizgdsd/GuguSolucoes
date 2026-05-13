# GuguSolucoes (Projeto Unificado)

Aplicativo desktop unificado para rotinas de TI no escritorio de contabilidade, juntando:

- modulo VoltaGOV (diagnostico/reparo de conectividade Gov.br),
- modulo LimpaCache (limpeza de temporarios manual e automatica).

## Base tecnica

- C# WinForms em .NET 8 (`WinExe`)
- instancia unica da interface via `Mutex` em `Program.cs`
- modo agente de limpeza no mesmo executavel via `--agent`
- icone do app e instalador: `favicon.ico`
- auto-update desabilitado por padrao, com desenho tecnico em `docs/autoupdate-design.md`

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
