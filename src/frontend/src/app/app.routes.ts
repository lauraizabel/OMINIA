import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { guestGuard } from './core/auth/guest.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login').then((component) => component.Login),
    title: 'Sign in | Sales Portal',
  },
  {
    path: 'forbidden',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/auth/forbidden/forbidden').then((component) => component.Forbidden),
    title: 'Access denied | Sales Portal',
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/app-shell/app-shell').then((component) => component.AppShell),
    children: [
      {
        path: 'sales',
        loadComponent: () =>
          import('./features/sales/sales-placeholder/sales-placeholder').then(
            (component) => component.SalesPlaceholder,
          ),
        title: 'Sales | Sales Portal',
      },
      { path: '', pathMatch: 'full', redirectTo: 'sales' },
    ],
  },
  { path: '**', redirectTo: '' },
];
