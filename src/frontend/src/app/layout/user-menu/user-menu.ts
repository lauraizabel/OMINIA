import { ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';
import { Component, computed, inject, input, output } from '@angular/core';
import { OverlayMenuCoordinator } from '../../shared/overlay-menu-coordinator.service';
import { Icon } from '../../shared/ui/icon/icon';

@Component({
  selector: 'app-user-menu',
  imports: [Icon, OverlayModule],
  templateUrl: './user-menu.html',
  styleUrl: './user-menu.scss',
})
export class UserMenu {
  private readonly menuCoordinator = inject(OverlayMenuCoordinator);
  private readonly menuId = 'user-menu';

  readonly name = input.required<string>();
  readonly role = input.required<string>();
  readonly signOut = output<void>();
  readonly open = computed(() => this.menuCoordinator.isOpen(this.menuId));
  readonly overlayPositions: ConnectedPosition[] = [
    {
      originX: 'end',
      originY: 'bottom',
      overlayX: 'end',
      overlayY: 'top',
      offsetY: 8,
    },
    {
      originX: 'end',
      originY: 'top',
      overlayX: 'end',
      overlayY: 'bottom',
      offsetY: -8,
    },
  ];
  readonly initials = computed(() =>
    this.name()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0].toUpperCase())
      .join(''),
  );

  toggle(): void {
    this.menuCoordinator.toggle(this.menuId);
  }

  close(): void {
    this.menuCoordinator.close(this.menuId);
  }

  logout(): void {
    this.close();
    this.signOut.emit();
  }

  handleOverlayKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
    }
  }
}
