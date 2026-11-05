# CalcPro — Calculadora Científica para Windows

Aplicativo **nativo para Windows** (C# / WinForms, .NET Framework 4 que já vem no Windows 10/11).
Não usa navegador nem precisa de instalação: basta executar `dist\CalcPro.exe`.

Inspirado nos recursos do Mobi Calculator (calculadora de expressões com histórico, RPN, bases etc.).

## Download

1. Vá em **[Releases](https://github.com/akcioamaral-ui/CalcPro/releases/latest)** e baixe o `CalcPro.exe`.
2. Dê dois cliques para abrir. Não precisa instalar; funciona no Windows 10 e 11.

> O programa não tem assinatura digital paga, então na primeira vez o Windows SmartScreen pode avisar.
> Clique em **Mais informações → Executar assim mesmo**. O código-fonte completo está neste repositório.

## Recursos

- **Expressão editável** com resultado em tempo real; Enter confirma.
- Prioridade de operações correta, multiplicação implícita (`2π`, `3(4+5)`), parênteses fechados automaticamente.
- **Porcentagem inteligente**: `200+10% = 220`, `50×10% = 5`. Em somas e subtrações, a linha abaixo da expressão mostra o passo: `50-3%` → `50-(1,5)`.
- **Abertura automática de ( [ {**: se você fechar sem ter aberto, a abertura é colocada sozinha (`56-3)` → `(56-3)`); fechar um `]` com `(` pendente fecha os dois (`[10-(4+2` + `]` → `[10-(4+2)]`).
- **Teclado científico opcional**: botão **f(x)** na barra superior (ou F4) mostra/oculta as funções, deixando só o teclado básico.
- Trigonometria (DEG/RAD/GRA), hiperbólicas, logaritmos, raízes, potências, fatorial/gama, nCr/nPr, MDC/MMC, min/max/soma/média, arredondamentos, aleatórios.
- **Graus-minutos-segundos**: entrada `30°15'20"` e exibição do resultado em GMS.
- **Frações**: o resultado mostra a fração equivalente (`0,75 → 3/4`).
- **Variáveis** (`x = 5`) e **funções próprias** (`área(r) = π r²`), salvas entre sessões.
- **Modo RPN** com pilha (ENTER, x⇄y, DROP).
- **Bases** HEX / OCT / BIN / DEC com AND, OR, XOR, NOT, <<, >> (inteiros de 64 bits).
- Formatos Normal / Científico / Engenharia / Fixo, precisão configurável, separador decimal vírgula ou ponto, separação de milhares.
- **Constantes** matemáticas e físicas (CODATA).
- **Histórico** pesquisável no painel lateral (duplo clique reutiliza, botão direito para mais opções).
- Tema escuro e claro, "sempre visível", tela em alta resolução (DPI).

## Atalhos

| Tecla | Ação |
|---|---|
| Enter / = | Calcular |
| Esc | Limpar |
| ↑ ↓ | Expressões anteriores |
| Ctrl+C (sem seleção) | Copiar resultado |
| F1 | Ajuda |
| F2 | 2nd (funções secundárias) |
| F4 | Mostrar/ocultar teclado científico |
| F5 / F6 / F7 / F8 | Ângulo / Base / ALG↔RPN / Formato |
| F9 | Histórico e variáveis |

## Usar como calculadora padrão

O Windows não permite trocar a calculadora padrão pelas configurações. O script abaixo faz o equivalente para o seu usuário (sem administrador):

```
powershell -ExecutionPolicy Bypass -File tools\definir-padrao.ps1
```

- A tecla **Calculadora** do teclado abre o CalcPro
- Atalho no Menu Iniciar com **Ctrl+Alt+C**
- **Win+R → `calcpro`**
- Se já estiver aberto, a janela existente vem para frente

Para desfazer: `tools\remover-padrao.ps1`. Se mover a pasta do programa, rode `definir-padrao.ps1` de novo.

## Compilar

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

Gera `dist\CalcPro.exe` e roda os testes do motor de cálculo (`tests\Tests.cs`).
Os dados do usuário ficam em `%APPDATA%\CalcPro\estado.txt`.

## Estrutura

- `src\Engine.cs` — analisador e avaliador de expressões
- `src\Format.cs` — formatação de números, frações, GMS, bases
- `src\Controls.cs` — temas e controles desenhados (teclas, visor, pilha)
- `src\MainForm.cs` — janela principal, teclado, RPN, menus, histórico, persistência
- `src\Program.cs` — ponto de entrada

## Licença

Distribuído sob a licença MIT — veja o arquivo `LICENSE`.

Autor: [akcioamaral-ui](https://github.com/akcioamaral-ui) · Sugestões e problemas: [Issues](https://github.com/akcioamaral-ui/CalcPro/issues)
