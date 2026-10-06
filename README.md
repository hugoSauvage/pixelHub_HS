# PixelHub — R5.A.10

Dépôt de départ du module *Nouveaux paradigmes de bases de données*. Une seule application, un moteur de plus à chaque séance.

## Démarrer

```bash
docker compose up -d          # lance les 4 moteurs
docker compose ps             # vérifier : 4 services "running"
cd src/PixelHub.Api
dotnet run                    # l'API écoute sur http://localhost:5199
```

Pour tester les endpoints : ouvrir `src/PixelHub.Api/PixelHub.Api.http` (Visual Studio, VS Code, Rider) et cliquer sur « Send request ». Il n'y a pas de page Swagger.

Ports (décalés volontairement pour éviter les conflits avec un moteur déjà installé) :

| Moteur | Port |
|---|---|
| PostgreSQL | 15432 |
| MongoDB | 27018 |
| Redis | 16379 |
| Neo4j — interface web | 17474 |
| Neo4j — Bolt (driver) | 17687 |

Identifiants de développement : `pixelhub` / `pixelhub_dev` (Neo4j : utilisateur `neo4j`).
Les ports se changent dans le fichier `.env` ; si vous en changez un, adaptez aussi `appsettings.json`.

## Arrêter

```bash
docker compose stop           # éteint, garde les données
```

⚠️ `docker compose down -v` supprime les données. À n'utiliser que pour repartir de zéro.

## Sans SDK .NET sur le poste

```bash
docker compose --profile app up -d --build   # moteurs + API conteneurisée sur http://localhost:5200
```
