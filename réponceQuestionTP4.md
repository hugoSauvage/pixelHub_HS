# TP 4 — Neo4j : réponses

## Partie 1 — Importer le graphe

### Q1. Pourquoi `MERGE` plutôt que `CREATE` ? À quoi servent les contraintes ?

`CREATE` crée systématiquement un nouveau nœud ou une nouvelle relation. Relancer un import écrit uniquement avec `CREATE` risquerait donc de créer des doublons : les relations, qui ne sont pas protégées par les contraintes de ce script, seraient recréées à chaque exécution. Les trois contraintes d’unicité empêcheraient toutefois de valider des nœuds `Joueur`, `Jeu` ou `Guilde` ayant un `pseudo`, un `titre` ou un `nom` déjà présent : Neo4j signalerait alors une violation de contrainte.

`MERGE` cherche d’abord un élément correspondant au motif fourni et ne le crée que s’il n’existe pas. Le script peut ainsi être relancé sans dupliquer les nœuds ni les relations décrits par ses motifs.

Les trois `CREATE CONSTRAINT ... IF NOT EXISTS` imposent l’unicité de `Joueur.pseudo`, `Jeu.titre` et `Guilde.nom`. Elles protègent l’intégrité des données et permettent à `MERGE` d’identifier sans ambiguïté les nœuds à réutiliser. `IF NOT EXISTS` évite de recréer une contrainte déjà installée.

### Q2. Quelle est l’importance de la direction d’une relation ?

La requête sans flèche renvoie **2 résultats** : `Nova` et `Pixel`. Celle avec une flèche renvoie **1 résultat** : `Pixel`.

Dans le script d’import, l’amitié entre Nova et Krayz est enregistrée comme `(Nova)-[:AMI_DE]->(Krayz)`, tandis que celle entre Krayz et Pixel est enregistrée comme `(Krayz)-[:AMI_DE]->(Pixel)`. La première relation arrive donc vers Krayz ; la seconde part de Krayz.

Pour trouver les amis de Krayz sans tenir compte du sens de stockage choisi par l’import, la requête sans flèche est correcte : elle correspond aux relations entrantes et sortantes. La requête avec `->` ne trouve que les relations sortantes de Krayz. Aucune erreur ne s’affiche car cette requête est valide en Cypher : elle demande simplement un sous-ensemble de relations et ne renvoie pas les relations entrantes.

### Q3. Que comptent les deux versions et que change une profondeur de 4 ?

`count(DISTINCT ami)` compte le nombre de joueurs différents qui possèdent le jeu recommandé. `count(*)` compte les correspondances trouvées par la requête : un même joueur peut compter plusieurs fois si plusieurs chemins d’amitié le relient à Nova.

Avec `*1..2`, il n’y a pas de chemin alternatif vers un même joueur parmi les amis et amis d’amis de Nova : les deux comptages donnent donc les mêmes valeurs. Les recommandations sont : Counter-Strike 2 (2), Valorant (2), Baldur's Gate 3 (2), Rocket League (1), Terraria (1), Mario Kart 8 Deluxe (1) et Vampire Survivors (1).

Passer de `*1..2` à `*1..4` ne change qu’une ligne de la requête : la borne de profondeur. Il y a toujours **7 jeux recommandés** avec `count(DISTINCT ami)`, mais davantage de joueurs peuvent contribuer : Rocket League, Terraria et Vampire Survivors passent à 2, et Counter-Strike 2 passe à 3 ; les autres nombres ne changent pas.

Avec `count(*)` et `*1..4`, certains joueurs accessibles par plusieurs chemins sont comptés plusieurs fois. Les nombres deviennent : Counter-Strike 2 (4), Valorant (3), Baldur's Gate 3 (3), Vampire Survivors (4), Rocket League (2), Terraria (2) et Mario Kart 8 Deluxe (1). En SQL, une CTE récursive permettrait de parcourir le graphe en répétant la même étape jusqu’à la profondeur maximale : on ajusterait sa condition de profondeur, sans nécessairement ajouter de CTE. Une solution SQL écrite avec des niveaux de jointures séparés devrait, elle, ajouter deux niveaux pour passer de 2 à 4.

### Q4. Pourquoi *Stardew Valley* n’est-il pas recommandé à Nova ?

Nova possède déjà *Stardew Valley* dans le script d’import. La condition `NOT (moi)-[:POSSEDE]->(j)` exclut donc ce jeu des recommandations, même si ses amis le possèdent aussi. *Hades II* est également exclu pour la même raison.

### Q5. Pourquoi deux ports pour le même serveur ?

Neo4j expose plusieurs protocoles sur des ports distincts. Le port **17474** est le port HTTP utilisé par Neo4j Browser, l’interface web ouverte dans le navigateur. Le port **17687** est le port Bolt, le protocole binaire utilisé par le driver Neo4j de l’application pour envoyer ses requêtes à la base. Ce sont deux points d’accès au même serveur, mais pour des usages et protocoles différents.

### Q6. Pourquoi interpoler la profondeur, et quelle précaution prendre ?

Dans cette requête, `$pseudo` est une valeur et peut donc être transmis comme paramètre Cypher. En revanche, la profondeur de `[:AMI_DE*1..N]` fait partie de la syntaxe du motif de parcours : elle ne peut pas être fournie comme paramètre ordinaire. Le code l’insère donc dans la chaîne de requête.

Le code n’accepte que les entiers de **1 à 4** et lève `ArgumentOutOfRangeException` hors de cette plage avant de construire la requête. Comme la profondeur est typée `int`, elle ne peut pas contenir de texte Cypher arbitraire ; le contrôle sert surtout à rejeter les valeurs hors plage et à borner le coût du parcours. Sans cette limite, une profondeur très élevée pourrait déclencher un parcours extrêmement coûteux. Si la profondeur était reçue comme texte et interpolée sans validation, elle pourrait aussi ouvrir un risque d’injection Cypher.

### Q7. Pourquoi un driver singleton et une session par opération ?

Le `IDriver` est un objet thread-safe et coûteux à créer : il gère les connexions et leur pool. On le partage donc comme singleton dans l’application, au lieu d’en créer un pour chaque requête HTTP. Une session est légère, conçue pour une opération, et n’est pas destinée à être partagée simultanément entre plusieurs opérations. Chaque méthode crée donc sa propre session et la libère avec `await using`, y compris en cas d’exception.

Même principe pour `MongoClient` et `ConnectionMultiplexer` : ce sont des clients thread-safe qui gèrent des connexions ; on les partage à longue durée de vie (singleton), plutôt que de les recréer à chaque opération. Les ressources de travail courtes, comme les sessions Neo4j, sont créées et libérées au plus près de leur utilisation.

