import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { salesRoleGuard } from './role.guard';

describe('salesRoleGuard', () => {
  const forbidden = { redirected: true };
  const router = { createUrlTree: vi.fn(() => forbidden) };

  it.each(['Manager', 'Admin'])('allows the %s role', (role) => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { session: () => ({ role }) } },
        { provide: Router, useValue: router },
      ],
    });
    expect(TestBed.runInInjectionContext(() => salesRoleGuard(null!, null!))).toBe(true);
  });

  it('routes a customer to the forbidden page', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { session: () => ({ role: 'Customer' }) } },
        { provide: Router, useValue: router },
      ],
    });
    expect(TestBed.runInInjectionContext(() => salesRoleGuard(null!, null!))).toBe(forbidden);
  });
});
