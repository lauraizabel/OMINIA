import { pendingChangesGuard } from './pending-changes.guard';

describe('pendingChangesGuard', () => {
  it('leaves a clean editor without prompting', () => {
    const confirm = vi.spyOn(window, 'confirm');
    expect(pendingChangesGuard({ hasPendingChanges: () => false }, null!, null!, null!)).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
    confirm.mockRestore();
  });

  it('uses an explicit confirmation for a dirty editor', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(pendingChangesGuard({ hasPendingChanges: () => true }, null!, null!, null!)).toBe(false);
    confirm.mockRestore();
  });
});
