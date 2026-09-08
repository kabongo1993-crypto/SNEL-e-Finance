/*
  SCRIPT D'INITIALISATION OPTIONNEL — NE PAS EXÉCUTER AUTOMATIQUEMENT

  Préféré : utiliser l'UI /login (mode bootstrap) ou POST /api/v1/auth/bootstrap
  lorsque UTILISATEUR est vide. Le mot de passe y est hashé via ASP.NET Identity PasswordHasher.

  Ce script n'est fourni que si un DBA souhaite insérer manuellement.
  Le MotDePasseHash DOIT être généré par PasswordHasher (format Identity V3),
  jamais en clair, jamais inventé à la main.

  Convention rôle User Admin Full (pas de table ROLE dans BD_SNEL) :
    Matricule = 'ADMIN-FULL'

  Exemple (à adapter — remplacer @Hash par un hash généré côté application) :

  INSERT INTO UTILISATEUR (
      Matricule, Nom, Prenom, NomUtilisateur, MotDePasseHash, Email, Actif, DateCreation
  )
  VALUES (
      'ADMIN-FULL',
      N'Administrateur',
      N'SNEL',
      'admin.snel',          -- choisir le login
      '<HASH_PASSWORDHASHER>', -- généré hors de ce fichier
      'admin@snel.cd',
      1,
      SYSUTCDATETIME()
  );

  Vérification :
  SELECT IdUtilisateur, NomUtilisateur, Matricule, Actif, LEN(MotDePasseHash) AS HashLen
  FROM UTILISATEUR;
*/
