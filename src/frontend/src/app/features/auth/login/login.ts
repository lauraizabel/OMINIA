import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem } from '../../../core/api/api.models';
import { AuthService } from '../../../core/auth/auth.service';
import { safeReturnUrl } from '../../../core/auth/return-url';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly passwordVisible = signal(false);

  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.auth
      .login(this.form.getRawValue())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          const returnUrl = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
          void this.router.navigateByUrl(returnUrl);
        },
        error: (error: unknown) => this.errorMessage.set(loginErrorMessage(error)),
      });
  }

  togglePasswordVisibility(): void {
    this.passwordVisible.update((visible) => !visible);
  }
}

function loginErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'We could not sign you in. Please try again.';
  }

  if (error.status === 0) {
    return 'The sales service is unavailable. Check your connection and try again.';
  }

  if (error.status === 401) {
    return 'The email or password is incorrect.';
  }

  if (error.status === 429) {
    return 'Too many sign-in attempts. Wait a moment and try again.';
  }

  const problem = error.error as Partial<ApiProblem> | null;
  return problem?.detail || 'We could not sign you in. Please try again.';
}
