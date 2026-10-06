# TP Bases de données : Réponses

> PostgreSQL · MongoDB · Redis · Neo4j · Docker Compose · EF Core

---

## Sommaire

| Partie | Questions |
|---|---|
|  Docker Compose & connexions | [Q1 à Q6](#-docker-compose--connexions) |
|  EF Core & PostgreSQL | [Q7 à Q8](#-ef-core--postgresql) |
|  Transactions | [Q9 à Q11](#-transactions) |
|  Redis | [Q12 à Q13](#-redis) |
|  Choix du modèle & Neo4j | [Q14 à Q16](#-choix-du-modèle--neo4j) |
|  Persistance des données | [Q17](#-persistance-des-données) |

---

## Docker Compose & connexions

### Q1. Pourquoi quatre moteurs dans le même fichier ?

Les quatre moteurs (**PostgreSQL, MongoDB, Redis, Neo4j**) correspondent aux différentes séances du TP. On les met dans le même fichier pour tout avoir au même endroit et lancer seulement ce dont on a besoin.

> Il y a aussi un service `api` qui ne se lance qu'avec le profil `app`.

### Q2. Quel service n'a pas de volume ?

C'est **Redis** : il n'a pas de section `volumes`. Ses données ne sont pas gardées en dehors du conteneur, donc si on le supprime ou le recrée, **on perd tout**.

### Q3. Que veut dire `"15432:5432"` ?

Le format est `port de la machine : port du conteneur`.

| Qui se connecte ? | Adresse à utiliser |
|---|---|
| Mon PC (l'hôte) | `localhost:15432` |
| Un autre conteneur du réseau Docker | `postgres:5432` (nom du service + port interne) |

Depuis mon PC je me connecte sur le **15432**, mais dans le conteneur PostgreSQL écoute sur le **5432**.

### Q4. Mot de passe en clair dans le fichier : acceptable ?

| Contexte | Verdict |
|---|---|
| TP en local | Ça peut passer si c'est juste pour tester, qu'il ne protège rien d'important et qu'on ne le réutilise pas ailleurs |
| roduction | Non : pas de mot de passe dans un fichier versionné (Git) et il faut des identifiants solides |

### Q5. Que fait `docker compose exec redis redis-cli ping` ?

`docker compose exec` lance une commande **dans un conteneur qui tourne déjà**. Ici `redis-cli ping` s'exécute dans le conteneur `redis`, pas sur mon PC, et Redis doit répondre `PONG`.

### Q6. `localhost` dans la chaîne de connexion

| Où tourne l'API ? | `localhost` désigne… | Chaîne de connexion |
|---|---|---|
| Sur mon PC (`dotnet run`) | mon PC | `Host=localhost;Port=15432` |
| Dans un conteneur | le conteneur de l'API lui-même | `Host=postgres;Port=5432` |

Avec `dotnet run`, Docker expose le port 5432 du conteneur sur le 15432 de ma machine, donc ça marche.

---

## EF Core & PostgreSQL

### Q7. Les trois joueurs sont-ils dupliqués au redémarrage ?

**Non.** Ils sont stockés dans PostgreSQL, dont les données sont dans le volume `pg_data`, pas dans la mémoire de l'appli. Au démarrage, le code n'ajoute les joueurs **que si la table `Players` est vide**, ce qui n'est plus le cas après le premier lancement.

### Q8. Valeur de `player.Id` avant / après `SaveChangesAsync()`

| Moment | Valeur de `player.Id` | Pourquoi |
|---|---|---|
| Avant | `0` | Valeur par défaut d'un `int`, c'est la base qui génère l'id |
| Après | `4` | EF Core récupère l'id donné par PostgreSQL (les ids 1, 2 et 3 sont déjà pris) |

> C'est **PostgreSQL** qui choisit 4 avec sa séquence, pas notre code.

---

## Transactions

### Q9. Effet du `ROLLBACK` et intérêt d'une transaction

Après le `ROLLBACK`, les soldes redeviennent comme avant le `BEGIN` : **Nova a 1200 pièces et Krayz 350**.

Sans transaction, chaque `UPDATE` est validé tout seul. Si le serveur s'éteint juste après le débit de Nova, elle perd 100 pièces sans que Krayz les reçoive, et les données sont fausses.

> Avec une transaction, les deux opérations sont **validées ensemble ou annulées ensemble**.

### Q10. Que voient B et l'API pendant que A n'a pas validé ?

- **Lecture** : B et l'API voient toujours la dernière version validée → Nova a **1200**, pas les 1100 de A.
- **Écriture** : si B essaie de modifier la même ligne, il doit **attendre** que A termine.
- ↩ Quand A fait `ROLLBACK`, son débit est annulé et B continue avec le solde validé.

Sans cette attente, deux achats en même temps pourraient partir du même solde et donner un résultat faux, ou dépenser plus que ce qu'il y a sur le compte.

### Q11. Qui refuse l'achat d'Ombre, et que se passe-t-il ensuite ?

C'est **PostgreSQL** qui refuse. La contrainte `CHECK` impose que `Coins` reste supérieur ou égal à 0, et le débit ferait passer Ombre de **90 à -10**.

1. Après l'erreur, la transaction est marquée comme **échouée**.
2. Les commandes suivantes (même le `SELECT`) sont refusées jusqu'au `ROLLBACK`.
3. Après le `ROLLBACK`, la contrainte n'existe plus : le `ALTER TABLE` était dans la même transaction et a été annulé avec.

> Ça montre que PostgreSQL peut aussi annuler des changements de structure comme `ALTER TABLE`.

---

## Redis

### Q12. `TTL` et `GET` sur une clé temporaire

| État de la clé | `TTL temporaire` | `GET temporaire` |
|---|---|---|
| Elle existe encore | nombre de secondes restantes (diminue avec le temps) | la valeur |
| Elle a expiré | `-2` (la clé n'existe plus) | `(nil)` |

**Exemple PixelHub** : un code de vérification temporaire ou un cache de classement irait bien pour une expiration.

### Q13. Pourquoi `INCR` plutôt que `GET` + calcul + `SET` ?

`INCR` est **atomique** : Redis le traite comme une seule opération, même si plusieurs clients arrivent en même temps.

Avec un `GET`, un calcul dans le code puis un `SET`, plusieurs joueurs pourraient lire la même valeur, calculer le même résultat et s'écraser entre eux. On perdrait des incréments, comme le problème des écritures en même temps vu avec les transactions et les verrous en 5.2.

---

## Choix du modèle & Neo4j

### Q14. Deux jeux de formes différentes : PostgreSQL ou MongoDB ?

**Oui**, on pourrait mettre les deux jeux dans PostgreSQL, mais il y a plusieurs façons de faire :

| Option | Avantage | Inconvénient |
|---|---|---|
| Une seule table avec colonnes facultatives (`plateformes`, `joueursMax`) | Simple | Plein de `NULL`, structure moins claire |
| Colonnes JSON / JSONB | Souple | Moins de contrôle sur la structure |
| Tables liées (normalisation, ex. une table pour les plateformes) | Meilleur contrôle des données | Plus de schéma, de relations et de jointures |

**MongoDB** accepte directement des documents de formes différentes dans la même collection : pratique pour un catalogue où les attributs changent selon le jeu, mais la structure est moins vérifiée par défaut.

> Ça rejoint l'exercice du catalogue de jeux du CM1 : le choix dépend de la variabilité des données et du besoin de cohérence et de requêtes.

### Q15. « Ami d'un ami » en SQL

On joint `Amities` **deux fois**, une par relation. Avec l'id d'Ana :

```sql
SELECT a2.ami_id
FROM Amities a1
JOIN Amities a2 ON a2.joueur_id = a1.ami_id
WHERE a1.joueur_id = :ana_id;
```

- **Ami d'un ami d'un ami** → trois jointures de `Amities`, une par étape du chemin.
- **Profondeur variable** → il faut en général une **requête récursive** en SQL.

### Q16. Pourquoi `DELETE` seul est refusé en Neo4j ?

Le `DELETE` seul est refusé parce que les nœuds `Essai` sont encore reliés par des relations `AMI_DE`. Neo4j interdit de supprimer un nœud qui a encore des relations, pour ne pas laisser de **relations orphelines**.

> `DETACH DELETE` supprime d'abord les relations du nœud, puis le nœud.

---

## 💾 Persistance des données

### Q17. `stop` / `start` contre `down` / `up`

| Donnée | `stop` puis `start` | `down` puis `up` |
|---|---|---|
| Clé Redis `survivant` | survit | perdue |
| Joueurs PostgreSQL | survivent | survivent |

**Pourquoi ?**

- `stop` arrête les conteneurs **sans les supprimer** : leur système de fichiers est toujours là.
- `down` **supprime** les conteneurs et le réseau, mais garde les volumes nommés par défaut.
- Redis n'a ni volume ni persistance ici, donc la clé est perdue après `down`.
- PostgreSQL stocke ses données dans le volume nommé `pg_data`, qui existe indépendamment du conteneur.

> Un volume, c'est un stockage qui ne dépend pas de la vie du conteneur.