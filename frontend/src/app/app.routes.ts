import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/live-map/live-map.component').then((m) => m.LiveMapComponent),
    title: 'BahaGuard AI - Metro Manila Live Flood Passability'
  },
  {
    path: 'audit',
    loadComponent: () =>
      import('./features/property-auditor/property-auditor.component').then(
        (m) => m.PropertyAuditorComponent
      ),
    title: 'BahaGuard AI - Property Flood-Risk Auditor'
  },
  {
    path: '**',
    redirectTo: ''
  }
];
