import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { guestGuard } from './core/auth/guest.guard';
import { salesRoleGuard } from './core/auth/role.guard';
import { pendingChangesGuard } from './features/sales/shared/pending-changes.guard';

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
        canActivate: [salesRoleGuard],
        loadComponent: () =>
          import('./features/sales/sales-list/sales-list').then((component) => component.SalesList),
        title: 'Sales | Sales Portal',
      },
      {
        path: 'sales/new',
        canActivate: [salesRoleGuard],
        canDeactivate: [pendingChangesGuard],
        loadComponent: () =>
          import('./features/sales/sale-editor/sale-editor').then(
            (component) => component.SaleEditor,
          ),
        title: 'New sale | Sales Portal',
      },
      {
        path: 'sales/:id/edit',
        canActivate: [salesRoleGuard],
        canDeactivate: [pendingChangesGuard],
        loadComponent: () =>
          import('./features/sales/sale-editor/sale-editor').then(
            (component) => component.SaleEditor,
          ),
        title: 'Edit sale | Sales Portal',
      },
      {
        path: 'sales/:id',
        canActivate: [salesRoleGuard],
        loadComponent: () =>
          import('./features/sales/sale-detail/sale-detail').then(
            (component) => component.SaleDetail,
          ),
        title: 'Sale details | Sales Portal',
      },
      { path: '', pathMatch: 'full', redirectTo: 'sales' },
    ],
  },
  { path: '**', redirectTo: '' },
];
