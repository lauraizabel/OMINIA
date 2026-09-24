import { Component } from '@angular/core';

@Component({
  selector: 'app-sales-placeholder',
  template: `
    <section class="page-heading">
      <div>
        <p>Sales workspace</p>
        <h1>Sales</h1>
        <span>Review and manage your commercial activity.</span>
      </div>
    </section>
    <section class="placeholder" aria-label="Sales workspace">
      <div class="placeholder-icon" aria-hidden="true">↗</div>
      <h2>Your workspace is ready.</h2>
      <p>Sales records and management actions will appear here.</p>
    </section>
  `,
  styles: `
    .page-heading {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 2rem;
      margin-bottom: 2rem;
    }
    .page-heading p {
      margin: 0 0 0.4rem;
      color: var(--primary);
      font-size: 0.72rem;
      font-weight: 850;
      letter-spacing: 0.12em;
      text-transform: uppercase;
    }
    h1 {
      margin: 0;
      color: var(--ink);
      font: 750 clamp(2.2rem, 5vw, 3.5rem)/1 var(--display-font);
      letter-spacing: -0.05em;
    }
    .page-heading span {
      display: block;
      margin-top: 0.65rem;
      color: var(--muted);
    }
    .placeholder {
      display: grid;
      min-height: 24rem;
      place-items: center;
      align-content: center;
      padding: 3rem;
      border: 1px dashed #b8c8d2;
      border-radius: 1rem;
      background: #fff;
      text-align: center;
    }
    .placeholder-icon {
      display: grid;
      width: 3.5rem;
      height: 3.5rem;
      place-items: center;
      margin-bottom: 1rem;
      border-radius: 1rem;
      color: var(--primary);
      background: #e0f5f0;
      font-size: 1.5rem;
    }
    h2 {
      margin: 0;
      color: var(--ink);
      font-family: var(--display-font);
      font-size: 1.5rem;
    }
    .placeholder p {
      max-width: 33rem;
      margin: 0.6rem 0 0;
      color: var(--muted);
      line-height: 1.55;
    }
    @media (max-width: 600px) {
      .page-heading {
        align-items: flex-start;
        flex-direction: column;
      }
    }
  `,
})
export class SalesPlaceholder {}
