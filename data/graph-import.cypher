// ---------- Contraintes ----------
CREATE CONSTRAINT joueur_pseudo IF NOT EXISTS
FOR (j:Joueur) REQUIRE j.pseudo IS UNIQUE;

CREATE CONSTRAINT jeu_titre IF NOT EXISTS
FOR (j:Jeu) REQUIRE j.titre IS UNIQUE;

CREATE CONSTRAINT guilde_nom IF NOT EXISTS
FOR (g:Guilde) REQUIRE g.nom IS UNIQUE;

// ---------- Joueurs ----------
UNWIND [
  {pseudo:'Nova', niveau:12}, {pseudo:'Krayz', niveau:8},
  {pseudo:'Ombre', niveau:15}, {pseudo:'Pixel', niveau:5},
  {pseudo:'Vega', niveau:20}, {pseudo:'Zed', niveau:11},
  {pseudo:'Luna', niveau:9},  {pseudo:'Rex', niveau:14},
  {pseudo:'Mika', niveau:7},  {pseudo:'Sora', niveau:17}
] AS d
MERGE (j:Joueur {pseudo: d.pseudo})
  ON CREATE SET j.niveau = d.niveau;

// ---------- Jeux ----------
UNWIND [
  'Counter-Strike 2', 'Cities: Skylines II', 'Stardew Valley',
  'Hades II', 'Mario Kart 8 Deluxe', "Baldur's Gate 3",
  'Rocket League', 'Terraria', 'Valorant', 'Vampire Survivors'
] AS titre
MERGE (:Jeu {titre: titre});

// ---------- Guildes ----------
UNWIND ['Les Pixels', 'Nuit Blanche'] AS nom
MERGE (:Guilde {nom: nom});

// ---------- Amitiés (une seule direction, interrogée sans flèche) ----------
UNWIND [
  ['Nova','Krayz'], ['Nova','Ombre'], ['Krayz','Pixel'],
  ['Ombre','Vega'], ['Vega','Zed'], ['Pixel','Luna'],
  ['Luna','Rex'], ['Zed','Mika'], ['Rex','Sora'], ['Ombre','Mika']
] AS p
MATCH (a:Joueur {pseudo: p[0]}), (b:Joueur {pseudo: p[1]})
MERGE (a)-[:AMI_DE]->(b);

// ---------- Possessions ----------
UNWIND [
  ['Nova','Stardew Valley'], ['Nova','Hades II'],
  ['Krayz','Counter-Strike 2'], ['Krayz','Valorant'], ['Krayz','Rocket League'],
  ['Ombre',"Baldur's Gate 3"], ['Ombre','Hades II'], ['Ombre','Terraria'],
  ['Pixel','Mario Kart 8 Deluxe'], ['Pixel','Stardew Valley'],
  ['Vega','Counter-Strike 2'], ['Vega','Valorant'],
  ['Zed','Vampire Survivors'], ['Zed','Hades II'],
  ['Luna','Terraria'], ['Luna','Stardew Valley'],
  ['Rex','Rocket League'], ['Rex','Counter-Strike 2'],
  ['Mika',"Baldur's Gate 3"], ['Mika','Vampire Survivors'],
  ['Sora','Cities: Skylines II'], ['Sora','Terraria']
] AS p
MATCH (j:Joueur {pseudo: p[0]}), (g:Jeu {titre: p[1]})
MERGE (j)-[:POSSEDE {depuis: 2024}]->(g);

// ---------- Appartenances ----------
UNWIND [
  ['Nova','Les Pixels'], ['Krayz','Les Pixels'], ['Pixel','Les Pixels'],
  ['Ombre','Nuit Blanche'], ['Vega','Nuit Blanche'], ['Zed','Nuit Blanche']
] AS p
MATCH (j:Joueur {pseudo: p[0]}), (g:Guilde {nom: p[1]})
MERGE (j)-[:MEMBRE_DE]->(g);
