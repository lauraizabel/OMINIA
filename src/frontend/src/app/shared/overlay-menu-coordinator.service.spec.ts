import { OverlayMenuCoordinator } from './overlay-menu-coordinator.service';

describe('OverlayMenuCoordinator', () => {
  it('keeps only the most recently opened menu active', () => {
    const coordinator = new OverlayMenuCoordinator();

    coordinator.toggle('date');
    expect(coordinator.isOpen('date')).toBe(true);

    coordinator.toggle('more');
    expect(coordinator.isOpen('date')).toBe(false);
    expect(coordinator.isOpen('more')).toBe(true);
  });

  it('closes only the menu identified by the caller', () => {
    const coordinator = new OverlayMenuCoordinator();
    coordinator.toggle('user');

    coordinator.close('date');
    expect(coordinator.isOpen('user')).toBe(true);

    coordinator.close('user');
    expect(coordinator.isOpen('user')).toBe(false);
  });
});
