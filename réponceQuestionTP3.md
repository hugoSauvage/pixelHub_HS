# TP 3 — Redis : réponses question par question

## Partie 1 — Structures Redis dans le shell

### Q1. `INCR` est-il atomique ? Que veut dire cela ?

Oui. `INCR compteur:parties` est une opération atomique : Redis la traite comme une seule instruction indivisible, sans qu'un autre client puisse la couper en deux.

Concrètement, si mille joueurs terminent une partie en même temps, Redis fait :

- lire la valeur actuelle,
- ajouter 1,
- écrire la nouvelle valeur,
- tout cela dans une seule opération au niveau du serveur.

Il n'y a donc pas de "lost update" où deux clients lisent la même valeur avant d'écrire.

En SQL, on peut faire un incrément directement dans une instruction :

```sql
UPDATE joueurs
SET nb_parties = nb_parties + 1
WHERE id = ...;
```

Cette instruction est également atomique côté base : PostgreSQL verrouille la ligne pendant la mise à jour, ce qui évite de perdre des incréments concurrents. En revanche, une séquence séparée `SELECT`, calcul dans l'application puis `UPDATE` peut perdre des mises à jour si elle n'est pas protégée par une transaction adaptée. Redis fournit directement `INCR` pour ce compteur, sans avoir à écrire soi-même cette séquence.

---

### Q2. Pourquoi ne pas tout mettre dans un `String` JSON ?

On pourrait faire :

```text
GET joueur:7
```

puis désérialiser le JSON, ajouter 250 XP, puis :

```text
SET joueur:7 "{...}" 
```

C'est ce qu'on appelle une lecture-modification-écriture complète.

Le souci : si deux requêtes arrivent en même temps :

1. Requête A lit `xp = 3400`
2. Requête B lit `xp = 3400`
3. A ajoute 250 → 3650 et écrit
4. B ajoute 250 → 3650 et écrase l'écriture de A

Résultat final : le gain de 250 a été perdu dans une écriture. On a un "race condition". Redis évite cela avec des opérations atomiques comme `HINCRBY` ou `INCRBY` sur une clé unique.

---

### Q3. Différence entre `ZRANGE` et `ZREVRANGE`

Un `sorted set` est ordonné par score :

- `ZRANGE ... WITHSCORES` trie du plus petit score au plus grand score
- `ZREVRANGE ... WITHSCORES` trie du plus grand score au plus petit score

Exemple :

- Nova = 4500
- Krayz = 3200
- Ombre = 5100
- Pixel = 1800

`ZREVRANGE classement:global 0 2 WITHSCORES` donne donc :

1. Ombre
2. Nova
3. Krayz

C'est la commande à utiliser pour afficher un "top 3".

---

### Q4. Que signifie `ZREVRANK` et que faut-il afficher au joueur ?

`ZREVRANK` renvoie un index de position dans le classement : par exemple `0`, `1`, `2` ...

Si on affiche ce nombre tel quel au joueur, il lira :

- "vous êtes en 0e position" ou "1e position" etc.

Cela est trompeur, car le classement humain commence généralement à 1. En plus, un `0` correspond au premier élément du classement dans la logique technique, pas à la position affichée.

Il faut donc afficher :

```text
rang + 1
```

et éventuellement donner un message du type :

- "Vous êtes 2e"
- "Vous êtes 3e"

---

### Q5. Qui sort de la file avec `RPOP` ? Pourquoi ?

On part de :

```text
LPUSH file:matchmaking "Nova"
LPUSH file:matchmaking "Krayz"
LPUSH file:matchmaking "Ombre"
```

La liste ressemble à :

```text
[Ombre, Krayz, Nova]
```

`RPOP` retire l'élément à droite, donc c'est **Nova** qui sort.

Pourquoi ? Parce que `LPUSH` ajoute à gauche, donc les éléments sont stockés dans l'ordre inverse des insertions, et le côté droit est le "bout de file" le plus ancien.

Avec `LPOP`, ce serait **Ombre** qui sortirait, car on retirerait à gauche.

---

### Q6. Valeurs retournées par `TTL` et signification

Sur une clé avec expiration (ex. `SET enligne:Nova 1 EX 30`) :

- au début, `TTL enligne:Nova` donne une valeur proche de `30`
- après 30 secondes, `TTL enligne:Nova` renvoie `-2`
- `EXISTS enligne:Nova` renvoie `0` si la clé a expiré

Pour une clé sans expiration, par exemple `compteur:parties` :

- `TTL compteur:parties` renvoie `-1`

Signification :

- `-1` : la clé n'a pas d'expiration
- `-2` : la clé n'existe plus (elle a expiré ou a été supprimée)
- un nombre positif : nombre de secondes restantes avant expiration

---

### Q7. Que faudrait-on écrire en PostgreSQL pour avoir le même comportement ?

En SQL, il faudrait écrire un nettoyage périodique :

```sql
DELETE FROM sessions
WHERE last_seen < NOW() - INTERVAL '30 seconds';
```

ou mieux, un job planifié (cron, tâche système, worker de fond) qui exécute cette requête toutes les quelques secondes ou minutes.

Les risques sont :

- risque de supprimer un utilisateur encore actif si le `last_seen` est ancien ou si le client ne l'a pas envoyé à temps,
- risque de race condition si plusieurs workers nettoient en même temps,
- coût de maintenance et de complexité plus élevé,
- parfois mauvaise cohérence si le nettoyage est fait au mauvais moment.

Redis rend ça simple grâce à la TTL automatique, sans avoir à faire de tâche de nettoyage manuelle.

---

## Partie 2 — Redis dans PixelHub

### Q8. Qu'est-ce qu'un `ConnectionMultiplexer` ?

`ConnectionMultiplexer` est la connexion réseau longue durée vers Redis, gérée par StackExchange.Redis.

Il garde :

- un pool de connexions,
- la gestion des reconnects,
- la multiplexion des commandes,
- et les opérations réseau optimisées.

Si on créait un `ConnectionMultiplexer` à chaque requête HTTP :

- on ouvrirait beaucoup de connexions TCP,
- on consommerait trop de mémoire et de CPU,
- on augmenterait le coût réseau,
- et on risquerait d'avoir des problèmes de performance ou de saturation.

Au TP, ce bug reste invisible car l'application est petite, peu chargée et testée localement. On ne voit pas encore la limite dans un environnement réel avec beaucoup de requêtes concurrentes.

---

### Q9. Que se passe-t-il si on enlève `Order.Descending` ?

Le tri devient croissant au lieu de décroissant.

Cela fait que le "top" affiche les mauvais scores en premier, et les meilleurs scores disparaissent du haut du classement.

C'est particulièrement dangereux parce que cela donne une vue fausse du classement à l'utilisateur, et que le système de hiérarchie devient totalement incohérent sans être forcément évident à repérer.

---

### Q10. Comment un joueur sort d'un `Set` quand il ferme son navigateur sans se déconnecter ?

Le problème est que les sets n'ont pas de TTL par membre. Redis ne propose pas de "supprimer un seul élément avec expiration indépendante" dans un set.

Il faut donc utiliser une structure qui permet d'associer un timestamp à chaque membre, puis de filtrer les anciens éléments.

La commande clé est :

```text
SREM
```

mais elle ne permet pas d'expirer automatiquement un seul membre d'un set. C'est donc la mauvaise structure pour ce besoin.

---

### Q11. Solution pour la présence en ligne : description avant codage

La bonne structure est un **sorted set**.

On choisit une clé du type :

```text
online:joueurs
```

Et on stocke :

- membre = pseudo du joueur
- score = timestamp Unix du dernier signal de vie

Exemple :

```text
ZADD online:joueurs 1710000000 "Nova"
```

Quand le joueur envoie un signe de vie :

```text
ZADD online:joueurs <timestamp> <pseudo>
```

Pour purger les joueurs dont le dernier signe de vie date d'au moins 60 secondes, puis compter ceux qui restent :

```text
ZREMRANGEBYSCORE online:joueurs -inf <now - 60>
ZCARD online:joueurs
```

Le score est le timestamp du dernier signe de vie ; les joueurs en ligne sont donc ceux dont le score est récent. Dans le code, on purge d'abord les anciens éléments, puis on compte les membres restants.

---

### Q12. Combien de lignes modifiées dans les endpoints ? Pourquoi ?

La réponse est : **0 ligne modifiée dans les endpoints**.

Le cache se met en place au niveau du conteneur de dépendances, sans toucher aux routes. L'application continue d'appeler `IGameCatalog`, et c'est l'enregistrement DI qui change :

- avant : `IGameCatalog` -> `MongoGameCatalog`
- après : `IGameCatalog` -> `CachedGameCatalog` qui délègue ensuite à `MongoGameCatalog`

Cela est possible grâce à la séance 2, où le code dépendait de l'interface `IGameCatalog` et non directement de MongoDB. C'est le bon choix de conception pour introduire un cache sans modifier le reste de l'application.

---

## Partie 3 — Mesurer et se méfier du benchmark

### Q13. Relevez les mesures. Le gain est-il spectaculaire ?

Sur cette machine, les mesures via l'API conteneurisée ont été d'environ **148 ms** pour le premier appel (cache miss) et **7 ms** pour le deuxième (cache hit). Le gain dépend de la taille du catalogue, de la latence du stockage et de la machine ; ces valeurs ne sont donc pas universelles.

Le gain n'est pas forcément "spectaculaire" en valeur absolue si le jeu est petit, mais il est réel et stable.

Conclusion honnête :

- le cache aide surtout quand on lit beaucoup la même donnée,
- quand le backend est plus lent (MongoDB, base distante, calculs lourds),
- et quand les données sont relativement stables.

Un cache devient décisif quand :

- la lecture est très fréquente,
- la source de vérité est coûteuse,
- ou le débit est important (milliers de requêtes / minute).

Sur ce petit catalogue local, le cache a tout de même réduit le temps mesuré d'environ 141 ms lors de ce test.

---

### Q14. Combien de temps verront les joueurs l'ancien titre après correction dans MongoDB ?

Les joueurs verront l'ancien titre pendant la durée de vie du cache, donc jusqu'à **60 secondes** dans ce TP (TTL du cache).

Pourquoi ? Parce que le cache Redis stocke la liste des jeux en mémoire. Tant que la clé n'est pas invalidée ou réécrite, Redis sert la version en cache, même si MongoDB a été modifié directement.

Deux stratégies pour réduire ce délai :

1. TTL plus court
   - Avantage : les données obsolètes expirent plus vite
   - Inconvénient : le cache est moins utile et plus souvent vidé, donc moins rentable

2. Invalidation explicite lors de l'écriture côté application
   - On supprime ou met à jour la clé Redis lorsqu'un titre est modifié
   - Avantage : les clients voient la nouvelle version plus vite
   - Inconvénient : il faut le faire dans tous les points d'écriture, et on peut oublier un cas ou avoir des incohérences

Autre variante possible : un mécanisme d'éviction / publication d'événement (cache-aside + invalidation), mais c'est plus complexe.

---

## Résumé rapide

- `INCR` est atomique : indispensable pour éviter les races.
- `HINCRBY` évite les problèmes de mise à jour concurrente sur un document JSON.
- `ZREVRANGE` est la bonne commande pour un top 3.
- `ZREVRANK` renvoie un index technique, pas un rang humain.
- `RPOP` sort le plus ancien de la liste si on utilise `LPUSH`.
- `TTL` vaut `-1` sans expiration, `-2` si la clé a expiré.
- Un `sorted set` est idéal pour la présence en ligne, avec timestamp en score.
- Le cache de catalogue n'a nécessité aucun changement dans les endpoints grâce à l'interface `IGameCatalog`.
- Le vrai gain d'un cache se mesure surtout sous forte charge et sur des accès coûteux.
- Le TTL du cache rend les données potentiellement obsolètes pendant quelques secondes, voire une minute.
