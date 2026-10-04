import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-page">
      <div class="login-card card">
        <div class="login-header">
          <div class="logo">🏥</div>
          <h2>Sistema de Triagem & Consultório</h2>
          <p class="subtitle">POC Acadêmica • Monólito Modular sem IA</p>
        </div>

        <div *ngIf="errorMessage" class="alert alert-danger">
          ⚠️ {{ errorMessage }}
        </div>

        <form (ngSubmit)="onSubmit()" class="login-form">
          <div class="form-group">
            <label class="form-label">E-mail</label>
            <input type="email" class="form-control" [(ngModel)]="email" name="email" required placeholder="seu.email@consultorio.local" />
          </div>

          <div class="form-group">
            <label class="form-label">Senha</label>
            <input type="password" class="form-control" [(ngModel)]="senha" name="senha" required placeholder="••••••••" />
          </div>

          <button type="submit" class="btn btn-primary btn-block" [disabled]="loading">
            {{ loading ? 'Autenticando...' : 'Entrar no Sistema' }}
          </button>
        </form>

        <div class="demo-accounts-section">
          <div class="demo-title">
            <span>⚡ Atalhos Rápidos para Apresentação / Banca</span>
          </div>
          <p class="demo-desc">Clique em qualquer conta fictícia abaixo para logar instantaneamente:</p>

          <div class="demo-grid">
            <button type="button" class="demo-btn pac" (click)="quickLogin('paciente.mariana@consultorio.local')">
              <span class="d-icon">👩</span>
              <span class="d-text">
                <strong>Mariana Souza</strong>
                <small>Paciente (Novo Fluxo ao Vivo)</small>
              </span>
            </button>

            <button type="button" class="demo-btn pac" (click)="quickLogin('paciente.carlos@consultorio.local')">
              <span class="d-icon">🧑</span>
              <span class="d-text">
                <strong>Carlos Silva</strong>
                <small>Paciente (Histórico + Anexo)</small>
              </span>
            </button>

            <button type="button" class="demo-btn enf" (click)="quickLogin('enfermeiro.santamaria@consultorio.local')">
              <span class="d-icon">🩺</span>
              <span class="d-text">
                <strong>Enf. Juliana Lima</strong>
                <small>Enfermeiro (Santa Maria)</small>
              </span>
            </button>

            <button type="button" class="demo-btn enf" (click)="quickLogin('enfermeiro.saobento@consultorio.local')">
              <span class="d-icon">🩺</span>
              <span class="d-text">
                <strong>Enf. Marcos Dias</strong>
                <small>Enfermeiro (São Bento)</small>
              </span>
            </button>

            <button type="button" class="demo-btn med" (click)="quickLogin('medico.santamaria@consultorio.local')">
              <span class="d-icon">👨‍⚕️</span>
              <span class="d-text">
                <strong>Dr. Carlos Roberto</strong>
                <small>Cardiologista (Santa Maria)</small>
              </span>
            </button>

            <button type="button" class="demo-btn med" (click)="quickLogin('medico.saobento@consultorio.local')">
              <span class="d-icon">👩‍⚕️</span>
              <span class="d-text">
                <strong>Dra. Ana Paula</strong>
                <small>Ortopedista (São Bento)</small>
              </span>
            </button>

            <button type="button" class="demo-btn atd" (click)="quickLogin('atendente.santamaria@consultorio.local')">
              <span class="d-icon">📋</span>
              <span class="d-text">
                <strong>Atd. Beatriz Alves</strong>
                <small>Atendente (Santa Maria)</small>
              </span>
            </button>

            <button type="button" class="demo-btn adm" (click)="quickLogin('admin@consultorio.local')">
              <span class="d-icon">⚙️</span>
              <span class="d-text">
                <strong>Admin Técnico</strong>
                <small>Administração</small>
              </span>
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #f0f9ff 0%, #e0f2fe 100%);
      padding: 1.5rem;
    }
    .login-card {
      width: 100%;
      max-width: 580px;
      box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1);
    }
    .login-header {
      text-align: center;
      margin-bottom: 1.5rem;
    }
    .logo {
      font-size: 3rem;
      margin-bottom: 0.5rem;
    }
    .login-header h2 {
      font-size: 1.375rem;
      font-weight: 700;
      color: var(--secondary);
    }
    .subtitle {
      font-size: 0.875rem;
      color: var(--text-muted);
      margin-top: 0.25rem;
    }
    .btn-block {
      width: 100%;
      margin-top: 0.5rem;
    }
    .demo-accounts-section {
      margin-top: 2rem;
      border-top: 1px solid var(--border);
      padding-top: 1.25rem;
    }
    .demo-title {
      font-size: 0.8125rem;
      font-weight: 700;
      color: #475569;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: 0.25rem;
    }
    .demo-desc {
      font-size: 0.8125rem;
      color: var(--text-muted);
      margin-bottom: 1rem;
    }
    .demo-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 0.625rem;
    }
    .demo-btn {
      display: flex;
      align-items: center;
      gap: 0.625rem;
      padding: 0.5rem 0.75rem;
      background: #f8fafc;
      border: 1px solid var(--border);
      border-radius: 8px;
      cursor: pointer;
      text-align: left;
      transition: all 0.15s ease;
    }
    .demo-btn:hover {
      transform: translateY(-1px);
      box-shadow: 0 2px 4px rgba(0,0,0,0.05);
    }
    .demo-btn.pac:hover { border-color: var(--primary); background: var(--primary-light); }
    .demo-btn.enf:hover { border-color: #8b5cf6; background: #ede9fe; }
    .demo-btn.med:hover { border-color: #10b981; background: #d1fae5; }
    .demo-btn.atd:hover { border-color: #f59e0b; background: #fef3c7; }
    .demo-btn.adm:hover { border-color: #ef4444; background: #fee2e2; }
    .d-icon { font-size: 1.25rem; }
    .d-text { display: flex; flex-direction: column; line-height: 1.2; }
    .d-text strong { font-size: 0.8125rem; color: var(--secondary); }
    .d-text small { font-size: 0.6875rem; color: var(--text-muted); }
  `]
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  email = '';
  senha = '';
  loading = false;
  errorMessage = '';

  quickLogin(email: string) {
    this.email = email;
    this.senha = 'Senha@123';
    this.onSubmit();
  }

  onSubmit() {
    if (!this.email || !this.senha) {
      this.errorMessage = 'Informe e-mail e senha.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    this.auth.login(this.email, this.senha).subscribe({
      next: (res) => {
        this.loading = false;
        this.redirectUserByRole(res.usuario.perfil);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.mensagem || 'Falha ao autenticar. Verifique as credenciais.';
      }
    });
  }

  private redirectUserByRole(perfil: string) {
    switch (perfil) {
      case 'Paciente':
        this.router.navigate(['/paciente/triagem']);
        break;
      case 'Enfermeiro':
        this.router.navigate(['/enfermeiro/fila']);
        break;
      case 'Medico':
        this.router.navigate(['/medico/consultas']);
        break;
      case 'Atendente':
        this.router.navigate(['/atendente/dashboard']);
        break;
      case 'Admin':
        this.router.navigate(['/admin/usuarios']);
        break;
      default:
        this.router.navigate(['/login']);
    }
  }
}
