/*
  SCRIPT OPTIONNEL — index consultation historique DPM

  Accélère GetHistoriqueConsultationAsync / GetHistoriqueAsync par (Entite, IdEntite).
  Vérifier l'absence de l'index avant exécution :

  SELECT name FROM sys.indexes
  WHERE object_id = OBJECT_ID(N'dbo.JOURNAL_AUDIT')
    AND name = N'IX_JOURNAL_AUDIT_Entite_IdEntite';
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.JOURNAL_AUDIT')
      AND name = N'IX_JOURNAL_AUDIT_Entite_IdEntite')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_JOURNAL_AUDIT_Entite_IdEntite]
        ON [dbo].[JOURNAL_AUDIT] ([Entite], [IdEntite])
        INCLUDE ([DateHeure], [Operation], [AnciennesValeurs], [NouvellesValeurs]);
END
GO
