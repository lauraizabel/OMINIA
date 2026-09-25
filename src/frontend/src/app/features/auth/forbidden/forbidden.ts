import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonDirective } from '../../../shared/ui/button/button.directive';

@Component({
  selector: 'app-forbidden',
  imports: [ButtonDirective, RouterLink],
  template: `
    <main class="status-page">
      <div class="status-card">
        <span class="status-code">403</span>
        <h1>You do not have access to this area.</h1>
        <p>Your account is signed in, but its role does not allow this operation.</p>
        <a appButton routerLink="/sales">Return to sales</a>
      </div>
    </main>
  `,
  styles: `
    :host {
      display: block;
      min-height: 100dvh;
    }
    .status-page {
      display: grid;
      min-height: 100dvh;
      place-items: center;
      padding: 2rem;
      background: var(--surface);
    }
    .status-card {
      max-width: 34rem;
      text-align: center;
    }
    .status-code {
      color: var(--primary);
      font-size: 0.8rem;
      font-weight: 900;
      letter-spacing: 0.16em;
    }
    h1 {
      margin: 0.8rem 0;
      color: var(--ink);
      font: 750 clamp(2rem, 6vw, 3.5rem)/1.05 var(--display-font);
      letter-spacing: -0.045em;
    }
    p {
      margin: 0 auto 2rem;
      color: var(--muted);
      line-height: 1.6;
    }
  `,
})
export class Forbidden {}
