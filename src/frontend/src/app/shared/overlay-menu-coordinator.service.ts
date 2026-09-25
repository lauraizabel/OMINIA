import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class OverlayMenuCoordinator {
  private readonly activeMenu = signal<string | null>(null);

  isOpen(menuId: string): boolean {
    return this.activeMenu() === menuId;
  }

  toggle(menuId: string): void {
    this.activeMenu.update((activeMenu) => (activeMenu === menuId ? null : menuId));
  }

  close(menuId?: string): void {
    if (!menuId || this.activeMenu() === menuId) {
      this.activeMenu.set(null);
    }
  }
}
