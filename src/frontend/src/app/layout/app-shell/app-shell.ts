import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ToastViewport } from '../../shared/ui/toast/toast-viewport';
import { UserMenu } from '../user-menu/user-menu';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ToastViewport, UserMenu],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
})
export class AppShell {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly session = this.auth.session;

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
