import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { OverlayMenuCoordinator } from '../shared/overlay-menu-coordinator.service';
import { AppShell } from './app-shell/app-shell';
import { UserMenu } from './user-menu/user-menu';

describe('application layout', () => {
  it('shows the active identity and signs out before navigating to login', () => {
    const logout = vi.fn();
    TestBed.configureTestingModule({
      imports: [AppShell],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            session: signal({
              token: 'token',
              email: 'manager@example.com',
              name: 'Sales Manager',
              role: 'Manager',
            }),
            logout,
          },
        },
      ],
    });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(AppShell);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Sales Manager');
    fixture.componentInstance.logout();
    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('coordinates the user menu, derives initials, and emits sign out', () => {
    TestBed.configureTestingModule({ imports: [UserMenu] });
    const fixture = TestBed.createComponent(UserMenu);
    const component = fixture.componentInstance;
    fixture.componentRef.setInput('name', '  Sales Manager  ');
    fixture.componentRef.setInput('role', 'Manager');
    const signOut = vi.fn();
    component.signOut.subscribe(signOut);
    fixture.detectChanges();

    expect(component.initials()).toBe('SM');
    component.toggle();
    fixture.detectChanges();
    expect(component.open()).toBe(true);
    expect(document.body.textContent).toContain('Sign out');

    const preventDefault = vi.fn();
    component.handleOverlayKeydown({ key: 'Enter', preventDefault } as unknown as KeyboardEvent);
    expect(preventDefault).not.toHaveBeenCalled();
    component.handleOverlayKeydown({ key: 'Escape', preventDefault } as unknown as KeyboardEvent);
    expect(component.open()).toBe(false);
    expect(preventDefault).toHaveBeenCalledOnce();

    component.toggle();
    component.logout();
    expect(signOut).toHaveBeenCalledOnce();
    expect(TestBed.inject(OverlayMenuCoordinator).isOpen('user-menu')).toBe(false);
  });
});
