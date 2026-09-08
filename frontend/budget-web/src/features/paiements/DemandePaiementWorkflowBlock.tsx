import { Box, Button, Chip, Collapse, Divider, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';

import type {

  DemandePaiementRetourDestinataire,

  DemandePaiementRoutage,

} from '../../services/apiClient';

import { efRadius } from '../../theme';

import { DemandePaiementAssignationBadge } from './DemandePaiementAssignationBadge';

import type { DemandeAssignationView } from './demandePaiementAssignationUtils';

import { buildWorkflowRoutageItems } from './demandePaiementRoutageUtils';

import { getWorkflowEtapeLabels } from './demandePaiementWorkflowUtils';

import { formatDateFr } from './paiementUtils';



type DemandePaiementWorkflowBlockProps = {

  statut: string | null | undefined;

  assignation?: DemandeAssignationView | null;

  routages?: DemandePaiementRoutage[] | null;

  retoursDestinataires?: DemandePaiementRetourDestinataire[] | null;

  routageLoading?: boolean;

  compact?: boolean;

};



export function DemandePaiementWorkflowBlock({

  statut,

  assignation = null,

  routages = null,

  retoursDestinataires = null,

  routageLoading = false,

  compact = false,

}: DemandePaiementWorkflowBlockProps) {

  const { statutLabel, etapePrecedente, etapeSuivante } = getWorkflowEtapeLabels(statut);

  const routageItems = buildWorkflowRoutageItems(routages, retoursDestinataires);

  const hasRoutageHistorique = routageItems.length > 0;

  const [circuitOpen, setCircuitOpen] = useState(false);

  if (compact) {
    return (
      <Paper
        variant="outlined"
        sx={{
          p: 1.25,
          borderRadius: `${efRadius.md}px`,
          bgcolor: 'var(--ef-surface-secondary)',
        }}
      >
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={1.25}
          useFlexGap
          sx={{ alignItems: { md: 'center' }, flexWrap: 'wrap' }}
        >
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, letterSpacing: 0.4 }}>
            WORKFLOW
          </Typography>
          <Typography variant="body2" sx={{ fontWeight: 800 }}>
            {statutLabel}
          </Typography>
          {assignation ? (
            <DemandePaiementAssignationBadge assignation={assignation} />
          ) : (
            <Typography variant="caption" color="text.secondary">
              Assignation non disponible
            </Typography>
          )}
          {!hasRoutageHistorique && etapeSuivante && (
            <Typography variant="caption" color="text.secondary">
              Suite : {etapeSuivante}
            </Typography>
          )}
          {(hasRoutageHistorique || routageLoading) && (
            <Button size="small" onClick={() => setCircuitOpen((open) => !open)} sx={{ ml: { md: 'auto' } }}>
              {circuitOpen ? 'Masquer le circuit' : 'Afficher le circuit'}
            </Button>
          )}
        </Stack>
        <Collapse in={circuitOpen} timeout={180}>
          <Box sx={{ mt: 1.25 }}>
            {routageLoading && (
              <Typography variant="body2" color="text.secondary">
                Chargement de l&apos;historique de routage…
              </Typography>
            )}
            {hasRoutageHistorique && (
              <Stack spacing={1} divider={<Divider flexItem />}>
                {routageItems.map((item) => (
                  <Box key={item.idRoutage}>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>
                      {item.actionLabel}
                      {item.isRetour ? ' · Retour' : ''}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {item.statutTransition}
                      {item.acteurs ? ` · ${item.acteurs}` : ''}
                      {` · ${formatDateFr(item.dateRoutage)}`}
                    </Typography>
                  </Box>
                ))}
              </Stack>
            )}
          </Box>
        </Collapse>
      </Paper>
    );
  }



  return (

    <Paper

      variant="outlined"

      sx={{

        p: { xs: 2, md: 2.5 },

        borderRadius: `${efRadius.md}px`,

        bgcolor: 'var(--ef-surface-secondary)',

      }}

    >

      <Stack spacing={1.5}>

        <Typography variant="overline" color="text.secondary">

          Workflow

        </Typography>



        <WorkflowRow label="Statut" value={statutLabel} emphasize />



        <Box>

          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>

            Assignation

          </Typography>

          {assignation ? (

            <DemandePaiementAssignationBadge assignation={assignation} />

          ) : (

            <Typography variant="body2" color="text.secondary">

              Non disponible

            </Typography>

          )}

        </Box>



        {routageLoading && (

          <Typography variant="body2" color="text.secondary">

            Chargement de l&apos;historique de routage…

          </Typography>

        )}



        {hasRoutageHistorique ? (

          <Box>

            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.75 }}>

              Historique de routage

            </Typography>

            <Stack spacing={1.25} divider={<Divider flexItem />}>

              {routageItems.map((item) => (

                <Box key={item.idRoutage}>

                  <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', mb: 0.25 }}>

                    <Typography variant="body2" sx={{ fontWeight: 700 }}>

                      {item.actionLabel}

                    </Typography>

                    {item.isRetour && (

                      <Chip size="small" color="warning" variant="outlined" label="Retour" />

                    )}

                    {item.estActif && (

                      <Chip size="small" color="info" variant="outlined" label="Actif" />

                    )}

                  </Stack>

                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>

                    {item.statutTransition}

                  </Typography>

                  {item.retourLabel && (

                    <Typography

                      variant="body2"

                      sx={{ mt: 0.25, fontWeight: 650, color: 'warning.dark' }}

                    >

                      {item.retourLabel}

                    </Typography>

                  )}

                  {item.acteurs && (

                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>

                      {item.acteurs}

                    </Typography>

                  )}

                  {item.motif && (

                    <Typography variant="caption" sx={{ display: 'block', mt: 0.25 }}>

                      Motif : {item.motif}

                    </Typography>

                  )}

                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>

                    {formatDateFr(item.dateRoutage)}

                  </Typography>

                </Box>

              ))}

            </Stack>

          </Box>

        ) : (

          !routageLoading && (

            <>

              {etapePrecedente && <WorkflowRow label="Étape précédente" value={etapePrecedente} />}

              {etapeSuivante && <WorkflowRow label="Étape suivante" value={etapeSuivante} />}

            </>

          )

        )}

      </Stack>

    </Paper>

  );

}



function WorkflowRow({

  label,

  value,

  emphasize = false,

}: {

  label: string;

  value: string;

  emphasize?: boolean;

}) {

  return (

    <Stack direction="row" spacing={1} sx={{ justifyContent: 'space-between', gap: 1 }}>

      <Typography variant="body2" color="text.secondary" sx={{ flexShrink: 0 }}>

        {label}

      </Typography>

      <Typography

        variant="body2"

        sx={{

          fontWeight: emphasize ? 800 : 650,

          textAlign: 'right',

          wordBreak: 'break-word',

        }}

      >

        {value}

      </Typography>

    </Stack>

  );

}


