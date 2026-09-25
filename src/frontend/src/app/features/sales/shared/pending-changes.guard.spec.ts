import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../../core/auth/auth.service';
import { pendingChangesGuard } from './pending-changes.guard';

describe('pendingChangesGuard', () => {
  const auth = { isAuthenticated: vi.fn(() => true) };

  beforeEach(() => {
    auth.isAuthenticated.mockReturnValue(true);
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }] });
  });

  function run(hasPendingChanges: boolean): boolean {
    return TestBed.runInInjectionContext(() =>
      pendingChangesGuard({ hasPendingChanges: () => hasPendingChanges }, null!, null!, null!),
    ) as boolean;
  }

  it('leaves a clean editor without prompting', () => {
    const confirm = vi.spyOn(window, 'confirm');
    expect(run(false)).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
    confirm.mockRestore();
  });

  it('uses an explicit confirmation for a dirty editor', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(run(true)).toBe(false);
    confirm.mockRestore();
  });

  it('does not trap a dirty editor after the session expires', () => {
    auth.isAuthenticated.mockReturnValue(false);
    const confirm = vi.spyOn(window, 'confirm');

    expect(run(true)).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
    confirm.mockRestore();
  });
});
