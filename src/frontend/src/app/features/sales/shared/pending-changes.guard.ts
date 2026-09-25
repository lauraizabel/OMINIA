import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

export interface PendingChangesAware {
  hasPendingChanges(): boolean;
}

export const pendingChangesGuard: CanDeactivateFn<PendingChangesAware> = (component) =>
  !inject(AuthService).isAuthenticated() ||
  !component.hasPendingChanges() ||
  window.confirm('Discard your unsaved changes?');
