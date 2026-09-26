import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import { Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let component: Login;
  let http: HttpTestingController;
  const navigateByUrl = vi.fn().mockResolvedValue(true);

  beforeEach(async () => {
    navigateByUrl.mockClear();
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
        { provide: Router, useValue: { navigateByUrl } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap({ returnUrl: '/sales?_page=2' }) },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('shows field errors without sending an invalid form', () => {
    component.submit();
    fixture.detectChanges();

    expect(component.form.controls.email.touched).toBe(true);
    expect(component.form.controls.password.touched).toBe(true);
    expect(fixture.nativeElement.querySelectorAll('.field-error')).toHaveLength(2);
    http.expectNone('/api/auth');
  });

  it('signs in once and returns to the requested internal deep link', () => {
    component.form.setValue({ email: 'manager@example.com', password: 'secret' });
    component.submit();

    const request = http.expectOne('/api/auth');
    request.flush({
      success: true,
      message: 'Authenticated',
      data: {
        token: 'signed-token',
        email: 'manager@example.com',
        name: 'Sales Manager',
        role: 'Manager',
      },
    });

    expect(navigateByUrl).toHaveBeenCalledWith('/sales?_page=2');
    expect(component.submitting()).toBe(false);
  });

  it('keeps the form and presents a useful message for invalid credentials', () => {
    component.form.setValue({ email: 'manager@example.com', password: 'wrong' });
    component.submit();
    http
      .expectOne('/api/auth')
      .flush(
        { type: 'AuthenticationError', detail: 'Invalid credentials.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('The email or password is incorrect.');
    expect(component.form.getRawValue()).toEqual({
      email: 'manager@example.com',
      password: 'wrong',
    });
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it.each([
    [429, null, 'Too many sign-in attempts'],
    [400, { detail: 'The account request is invalid.' }, 'account request is invalid'],
    [500, null, 'We could not sign you in'],
  ])('maps HTTP status %s to an actionable message', (status, error, message) => {
    component.form.setValue({ email: 'manager@example.com', password: 'secret' });
    component.submit();
    http.expectOne('/api/auth').flush(error, { status, statusText: 'Failure' });
    expect(component.errorMessage()).toContain(message);
  });

  it('handles a network failure and toggles password visibility', () => {
    component.form.setValue({ email: 'manager@example.com', password: 'secret' });
    component.submit();
    http.expectOne('/api/auth').error(new ProgressEvent('network'));
    expect(component.errorMessage()).toContain('sales service is unavailable');
    expect(component.passwordVisible()).toBe(false);
    component.togglePasswordVisibility();
    expect(component.passwordVisible()).toBe(true);
    component.togglePasswordVisibility();
    expect(component.passwordVisible()).toBe(false);
  });
});
