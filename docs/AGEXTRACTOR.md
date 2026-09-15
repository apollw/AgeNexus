# AgeXtractor integrado

O AgeNexus incorpora o núcleo Python do repositório `apollw/AgeXtractor` no mesmo contêiner do serviço web. O Docker fixa uma revisão imutável do projeto, instala Python, OpenCV headless e Tesseract e configura o caminho do runtime. Não existe um segundo serviço HTTP.

## Fluxo

Na tela de desempenho de uma partida em rascunho, o administrador pode carregar as capturas de Placar, Militar, Economia, Tecnologia e Sociedade uma por vez ou em conjunto. Não é necessário selecionar as cinco para começar. Cada captura é visualizada localmente no navegador para que os quatro cantos externos da tabela sejam ajustados, reproduzindo a correção de perspectiva da interface Windows. A quantidade de jogadores vem dos participantes humanos da partida. Cada imagem pode ter até 5 MB e precisa ser JPEG, PNG ou WebP.

Ao iniciar a extração individual ou das imagens carregadas, o navegador envia as coordenadas da imagem original junto dos bytes mantidos em memória. Cada categoria é executada isoladamente com `--categoria`; seu fragmento JSON bem-sucedido permanece na memória da página enquanto as demais são processadas. Uma falha não elimina categorias concluídas, e uma nova tentativa executa somente as pendentes. O administrador pode remover uma imagem e selecionar outra; nesse caso, o fragmento daquela categoria também é invalidado. Também pode descartar todos os fragmentos e recomeçar mantendo as imagens e delimitações.

O servidor valida a assinatura do arquivo e os pontos, cria uma pasta temporária com nome interno e um `regioes.json`, executa `python -m agextractor.interfaces.server` e recebe o fragmento por `stdout`. A pasta é removida depois de cada categoria. Apenas uma categoria é processada por vez em cada instância do AgeNexus e cada tentativa tem limite de cinco minutos. Somente quando as cinco terminam, `AgeExtractorJsonComposer` valida as categorias e a quantidade de jogadores e consolida um único JSON completo no contrato original. O fluxo online nunca encaminha um JSON parcial para importação.

Os eventos estruturados recebidos por `stderr` atualizam a barra de progresso com categoria, jogador e campo em processamento. Em uma falha esperada de reconhecimento, a página apresenta a categoria e o motivo devolvido pelo Python, sem traceback, caminhos internos ou conteúdo das capturas. O JSON final continua isolado em `stdout`.

O JSON passa pelo mesmo `AgeExtractorJsonImporter` usado no upload manual. Leituras ausentes ou duvidosas permanecem visíveis, as posições precisam ser associadas aos jogadores e nada é salvo automaticamente. O administrador revisa os valores antes de preencher e salvar o relatório.

## Build e atualização

`AGEXTRACTOR_COMMIT` no `Dockerfile` identifica a revisão incorporada. Para atualizar o extrator, execute primeiro seus testes no repositório próprio, altere o SHA no AgeNexus e valide o build completo do contêiner. O CI do AgeNexus monta a imagem e confirma que Python, OpenCV, pytesseract e o pacote `agextractor` podem ser importados.

Em desenvolvimento fora do contêiner, configure `AgeExtractor__PythonExecutable` e `AgeExtractor__WorkingDirectory`. Sem esses caminhos válidos, a importação manual de JSON continua disponível e a extração online apresenta indisponibilidade.

## Operação

Erros e encerramentos do processo são registrados sem guardar as imagens ou o JSON nos logs. `Busy` indica outra extração em andamento; `Timeout`, processamento acima de cinco minutos; `Unavailable`, runtime ausente; e `InvalidImage`, arquivo inválido ou acima do limite. Falhas do processo incluem a última etapa concluída ou o diagnóstico estruturado do Python. CPU e memória devem ser observadas no Render durante as primeiras extrações reais.

As capturas, coordenadas e fragmentos do extrator não são enviados ao Supabase Storage nem gravados no PostgreSQL. A prévia utiliza um `object URL` local do navegador; no servidor, esse material existe somente na memória do circuito e na pasta temporária do processamento. Fechar ou recarregar a página descarta os resultados incrementais. A galeria pública de evidências é separada e só armazena uma captura quando o administrador a envia explicitamente nessa seção.
