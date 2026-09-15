# AgeXtractor integrado

O AgeNexus incorpora o núcleo Python do repositório `apollw/AgeXtractor` no mesmo contêiner do serviço web. O Docker fixa uma revisão imutável do projeto, instala Python, OpenCV headless e Tesseract e configura o caminho do runtime. Não existe um segundo serviço HTTP.

## Fluxo

Na tela de desempenho de uma partida em rascunho, o administrador seleciona as capturas de Placar, Militar, Economia, Tecnologia e Sociedade. A quantidade de jogadores vem dos participantes humanos da partida. Cada imagem pode ter até 5 MB e precisa ser JPEG, PNG ou WebP.

O servidor valida as assinaturas dos arquivos, cria uma pasta temporária com nomes internos, executa `python -m agextractor.interfaces.server` e recebe o contrato JSON por `stdout`. A pasta é removida ao final. Apenas uma extração é executada por instância do AgeNexus e cada tentativa tem limite de cinco minutos.

O JSON passa pelo mesmo `AgeExtractorJsonImporter` usado no upload manual. Leituras ausentes ou duvidosas permanecem visíveis, as posições precisam ser associadas aos jogadores e nada é salvo automaticamente. O administrador revisa os valores antes de preencher e salvar o relatório.

## Build e atualização

`AGEXTRACTOR_COMMIT` no `Dockerfile` identifica a revisão incorporada. Para atualizar o extrator, execute primeiro seus testes no repositório próprio, altere o SHA no AgeNexus e valide o build completo do contêiner. O CI do AgeNexus monta a imagem e confirma que Python, OpenCV, pytesseract e o pacote `agextractor` podem ser importados.

Em desenvolvimento fora do contêiner, configure `AgeExtractor__PythonExecutable` e `AgeExtractor__WorkingDirectory`. Sem esses caminhos válidos, a importação manual de JSON continua disponível e a extração online apresenta indisponibilidade.

## Operação

Erros e encerramentos do processo são registrados sem guardar as imagens ou o JSON nos logs. `Busy` indica outra extração em andamento; `Timeout`, processamento acima de cinco minutos; `Unavailable`, runtime ausente; e `InvalidImage`, arquivo inválido ou acima do limite. CPU e memória devem ser observadas no Render durante as primeiras extrações reais.
