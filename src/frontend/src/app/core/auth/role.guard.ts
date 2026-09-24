import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

const salesRoles = new Set(['Manager', 'Admin']);

export const salesRoleGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return salesRoles.has(auth.session()?.role ?? '')
    ? true
    : inject(Router).createUrlTree(['/forbidden']);
};
