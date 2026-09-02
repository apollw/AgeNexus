# Modelo conceitual

O diagrama registra entidades e relações planejadas. Nem todas fazem parte da primeira fatia.

```mermaid
erDiagram
    APPLICATION_USER o|--o| PLAYER_PROFILE : vincula
    PLAYER_PROFILE ||--o{ EXTERNAL_IDENTITY : possui
    GAME ||--o{ GAME_EDITION : possui
    GAME_EDITION ||--o{ FACTION : configura
    GAME_EDITION ||--o{ MAP_DEFINITION : configura
    GAME_EDITION ||--o{ AI_DIFFICULTY : configura
    MATCH }o--|| GAME_EDITION : usa
    MATCH ||--|{ MATCH_TEAM : contem
    MATCH_TEAM ||--|{ MATCH_PARTICIPANT : contem
    MATCH_PARTICIPANT }o--o| PLAYER_PROFILE : representa
    MATCH_PARTICIPANT }o--o| AI_DIFFICULTY : representa
    MATCH ||--o{ MATCH_EVIDENCE : comprova
    MATCH ||--o{ MATCH_CONFIRMATION : recebe
    MATCH ||--o{ MATCH_REVISION : audita
    MATCH ||--o| MATCH_STATISTICS_REPORT : detalha
    MATCH_STATISTICS_REPORT ||--|{ PLAYER_MATCH_STATISTICS : contem
    MATCH_STATISTICS_REPORT ||--o{ STATISTICS_CONFIRMATION : confirma
    MATCH_STATISTICS_REPORT ||--o{ PLAYER_PERFORMANCE_SCORE : calcula
    MATCH ||--o{ RATING_EVENT : origina
    MATCH ||--o{ CAREER_POINT_EVENT : origina
    PLAYER_PROFILE ||--o{ PLAYER_RATING : mantem
    TEAM_LINEUP ||--|{ TEAM_LINEUP_MEMBER : congela
```

- `Match` é a raiz do agregado; equipes e participantes não são alterados isoladamente.
- Um humano aponta para `PlayerProfile`, que pode ou não estar vinculado a uma conta.
- Uma IA aponta para dificuldade configurável e não é um usuário fictício.
- Resultado pertence à equipe. O formato (`3x2`) é derivado das quantidades de humanos.
- Rating e pontos são livros de eventos reconstruíveis e independentes.
- Relatórios pós-jogo preservam a origem dos dados e só geram bônus de carreira após confirmação e validação.
