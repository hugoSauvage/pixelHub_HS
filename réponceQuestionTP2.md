# TP 2 — MongoDB : réponses

## Partie 1 — MongoDB sans C#

### Q1. Qui crée le champ `_id` ?

Le fichier `games.json` ne contient pas de champ `_id`. Lors de l'import, `mongoimport` (via le driver MongoDB qu'il utilise) attribue un identifiant de type `ObjectId` aux documents qui n'en ont pas, avant de les envoyer à la base. Le serveur MongoDB reçoit et stocke donc ces identifiants ; ils n'ont pas été choisis dans le fichier source.

Pour *Pixel* au TP 1, c'était différent : l'`Id` était un entier généré par PostgreSQL à l'insertion. Avant `SaveChangesAsync()`, sa valeur était `0` (valeur par défaut en C#) ; après l'enregistrement, EF Core avait récupéré la valeur `4`, fournie par la séquence PostgreSQL. On ne connaissait donc cet identifiant qu'après l'enregistrement.

### Q2. Rechercher les jeux disponibles sur Switch en SQL normalisé

Dans un modèle relationnel normalisé, les plateformes seraient stockées dans une table séparée et reliées aux jeux par une table de jointure. On pourrait alors rechercher les jeux sur Switch avec des jointures :

```sql
SELECT j.titre
FROM jeux AS j
JOIN jeux_plateformes AS jp ON jp.jeu_id = j.id
JOIN plateformes AS p ON p.id = jp.plateforme_id
WHERE p.nom = 'Switch';
```

MongoDB peut, lui, tester directement si la valeur `"Switch"` est présente dans le tableau `plateformes` du document avec `{ plateformes: "Switch" }`.

### Q3. Pourquoi `--authenticationDatabase admin` ?

L'option indique à MongoDB dans quelle base chercher le compte utilisé pour s'authentifier. Le compte `pixelhub` est créé dans la base `admin` par l'image Docker officielle, grâce aux variables `MONGO_INITDB_ROOT_USERNAME` et `MONGO_INITDB_ROOT_PASSWORD` définies dans `docker-compose.yml`.

La base d'authentification (`admin`) est distincte de la base de données cible (`pixelhub`) et de sa collection `jeux`. Il faut donc préciser `--authenticationDatabase admin` pour que `mongoimport` et `mongosh` trouvent le compte au bon endroit.

### Q4. Schéma souple : bonne nouvelle ou problème ?

C'est à la fois un avantage et un risque. MongoDB accepte le nouveau document sans obliger tous les jeux à avoir les mêmes champs : c'est pratique si certains jeux ont des caractéristiques propres.

Mais dans une équipe de quatre développeurs, sans règles communes, chacun peut nommer ou typer les champs différemment. Six mois plus tard, une requête peut manquer des documents ou une fonctionnalité peut échouer parce qu'elle s'attend à un champ absent ou d'un type inattendu. Il faut donc documenter le modèle et, si nécessaire, ajouter une validation de schéma côté MongoDB ou dans l'application.

### Q5. Qu'arrive-t-il avec `db.Jeux.find(...)` ?

La collection s'appelle `jeux` avec un `j` minuscule. MongoDB distingue les majuscules et les minuscules dans les noms de collection : `Jeux` et `jeux` désignent donc deux collections différentes.

La commande est syntaxiquement valide ; MongoDB cherche dans `Jeux`, qui n'a pas les documents importés, et renvoie un curseur vide. Il n'y a pas d'erreur parce qu'une recherche dans une collection inexistante est autorisée et ne crée pas la collection. Il faut utiliser `db.jeux.find({ genre: "FPS" })`.

### Q6. Pourquoi la mise à jour sans `$set` échoue ?

`updateOne` attend un document de mise à jour contenant un opérateur, comme `$set`. Sans opérateur, MongoDB refuse la commande avec un message du type : `The update operation document must contain atomic operators`.

Pour modifier seulement la note, il faut écrire :

```javascript
db.jeux.updateOne({ titre: "Valorant" }, { $set: { note: 4.4 } })
```

Pour remplacer réellement tout le document, on utilise `replaceOne` et on fournit tous les champs que l'on veut conserver :

```javascript
db.jeux.replaceOne(
  { titre: "Valorant" },
  { titre: "Valorant", genre: "FPS", note: 4.4 }
)
```

Les autres champs du document existant ne figurant pas dans le document de remplacement seraient supprimés.

### Q7. Ajouter `nbVotes` en SQL

Pour ajouter une colonne à la table, on utiliserait par exemple :

```sql
ALTER TABLE jeux ADD COLUMN nb_votes INTEGER;
```

Cela modifie le schéma de toute la table. Les lignes déjà présentes auront `NULL` dans cette nouvelle colonne, sauf si une valeur par défaut est définie. Pour renseigner la valeur uniquement pour Terraria, il faudrait ensuite mettre à jour cette ligne :

```sql
UPDATE jeux
SET nb_votes = 0
WHERE titre = 'Terraria';
```

Contrairement à MongoDB, où l'on peut ajouter un champ à un seul document sans changer les autres, l'ajout d'une colonne SQL rend cette colonne disponible pour toutes les lignes.

### Q8. Pourquoi `IGameCatalog` ne mentionne pas MongoDB ?

Non, le mot « Mongo » n'apparaît pas dans l'interface. `IGameCatalog` décrit seulement les opérations disponibles sur le catalogue, sans révéler comment elles sont réalisées.

C'est important parce que le reste de l'application peut dépendre de cette interface plutôt que de MongoDB directement. On peut ainsi remplacer l'implémentation MongoDB par une autre (par exemple une implémentation en mémoire pour les tests) sans changer le code qui utilise le catalogue. Cela limite le couplage à la technologie de stockage.

### Q9. Pourquoi l'appel à `/games` échoue-t-il ?

L'appel renvoie une erreur HTTP 500 avec l'exception :

```text
System.FormatException: Element 'nombreArmes' does not match any field or property of class PixelHub.Api.Models.Game.
```

Le champ signalé est `nombreArmes`. Il existe dans certains documents du catalogue (par exemple celui de *Hades II*), mais pas comme propriété de la classe C# `Game`. En lisant les documents, le driver MongoDB essaie de désérialiser chaque champ BSON vers une propriété correspondante de `Game`. Comme il ne trouve pas de propriété pour `nombreArmes`, il ne sait pas où ranger cette valeur et lève l'exception.

MongoDB accepte des documents ayant des champs différents, mais le modèle C# est ici plus strict : il ne décrit que les champs communs. Il faut soit ajouter les propriétés nécessaires à `Game`, soit configurer le modèle pour ignorer les champs supplémentaires (par exemple avec `[BsonIgnoreExtraElements]`), selon les besoins de l'application.

### Q10. Que perd-on avec `[BsonIgnoreExtraElements]` ?

Cet attribut permet au driver de désérialiser les champs connus par `Game` et d'ignorer ceux qui n'ont pas de propriété correspondante. L'erreur disparaît, mais les champs supplémentaires ne sont pas conservés dans l'objet C# et ne sont donc pas renvoyés par l'API.

En particulier, `joueursParEquipe` existe dans le document MongoDB de *Counter-Strike 2*, mais pas dans la classe `Game`. Avec `[BsonIgnoreExtraElements]`, cette valeur reste stockée dans MongoDB, mais elle est ignorée lors de la désérialisation et n'apparaît pas dans la réponse de `/games`. Pour l'exposer dans l'API, il faudrait ajouter la propriété correspondante à `Game`.

### Q11. Comparer les approches pour gérer un schéma variable

| Approche | Avantage | Inconvénient |
|---|---|---|
| `[BsonIgnoreExtraElements]` | Solution simple : le modèle ne contient que les champs utilisés par l'application et la désérialisation ne plante pas sur les autres. | Les champs inconnus sont ignorés et perdus dans l'objet C# ; l'API ne peut pas les renvoyer ni les modifier. |
| `[BsonExtraElements]` | Capture les champs supplémentaires dans un `BsonDocument`, ce qui permet de les conserver et de les exposer sans déclarer chaque champ. | Les valeurs sont moins fortement typées et demandent une conversion pour être manipulées ou sérialisées par l'API ; il faut aussi gérer les changements de forme. |
| Hiérarchie de classes (`FpsGame : Game`, etc.) | Chaque catégorie peut avoir des propriétés explicites et typées, avec une structure claire pour le code métier. | Il faut définir et maintenir les sous-classes et leur discrimination ; cela devient lourd si les catégories ou leurs variantes sont nombreuses ou changent souvent. |

En résumé, ignorer les extras est adapté quand l'application n'en a pas besoin, les capturer convient pour conserver ou transmettre des champs variables, et une hiérarchie est intéressante quand les variantes sont connues et ont des comportements métier distincts.

### Q12. Agrégation MongoDB et équivalent SQL

Sur une table relationnelle `jeux(titre, genre, note)`, la requête équivalente serait :

```sql
SELECT genre, AVG(note) AS note_moyenne, COUNT(*) AS nombre
FROM jeux
GROUP BY genre
ORDER BY note_moyenne DESC;
```

L'agrégation MongoDB fait la même chose : elle regroupe les documents par genre, calcule la moyenne des notes et le nombre de jeux, puis trie les groupes par moyenne décroissante. PostgreSQL sait tout aussi bien réaliser ce calcul ; cette agrégation n'est donc pas, à elle seule, un avantage de MongoDB.

Dans ce TP, l'intérêt de MongoDB se situe plutôt dans la représentation du catalogue en documents dont les champs peuvent varier selon les jeux, ainsi que dans le stockage direct de valeurs imbriquées comme les tableaux `plateformes` et `tags`, sans devoir créer des tables séparées et des jointures pour ces données.

## Partie 4 — Imbriquer ou référencer

### Q13. Pourquoi les requêtes d et e ne donnent-elles pas le même résultat ?

La requête **e** est celle qui répond exactement à la question « les jeux où Krayz a mis 5 » :

```javascript
db.jeux.find(
  { avis: { $elemMatch: { auteur: "Krayz", note: 5 } } },
  { titre: 1, _id: 0 }
)
```

Elle exige qu'un même élément du tableau `avis` ait à la fois `auteur: "Krayz"` et `note: 5`. Elle ne renvoie aucun jeu dans les données de cet exercice.

La requête **d** impose séparément que `avis.auteur` contienne `"Krayz"` et que `avis.note` contienne `5`, mais ces deux conditions peuvent être satisfaites par deux avis différents. Elle renvoie donc *Stardew Valley* à tort pour la question posée : Krayz lui a donné 4, tandis que Nova lui a donné 5.

### Q14. Taille des avis imbriqués et limite BSON

La mesure de *Stardew Valley* est passée de **273 octets** avant les avis à **516 octets** après l'ajout des trois avis. L'augmentation est de 243 octets, soit environ **81 octets par avis** pour les avis de cet exemple.

En extrapolant simplement cette moyenne jusqu'à la limite de 16 777 216 octets, le document pourrait contenir environ **207 122 avis au total** (environ 207 119 avis supplémentaires à partir de la mesure actuelle). C'est une estimation : la taille réelle varie selon le texte de chaque avis et l'encodage BSON.

La limite n'est probablement pas un problème pour la plupart des jeux de taille modeste, mais elle peut devenir un vrai risque pour un jeu populaire si le tableau d'avis croît sans borne. Les critères du cours favorisent l'imbrication quand les données associées sont peu nombreuses, bornées et consultées avec leur parent. Pour une collection d'avis potentiellement très nombreuse, consultée ou modifiée indépendamment, mieux vaut référencer les avis dans une collection séparée.
