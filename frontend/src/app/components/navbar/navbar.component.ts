import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `
    <nav class="navbar" *ngIf="auth.currentUser() as user">
      <div class="nav-container">
        <div class="nav-left">
          <div class="brand">
            <span class="brand-icon">🏥</span>
            <span class="brand-title">Consultório POC</span>
          </div>

          <div class="nav-links">
            <!-- Links por perfil -->
            <ng-container [ngSwitch]="user.perfil">
              <ng-container *ngSwitchCase="'Paciente'">
                <a routerLink="/paciente/triagem" routerLinkActive="active" class="nav-link">💬 Triagem (Chat)</a>
                <a routerLink="/paciente/agenda" routerLinkActive="active" class="nav-link">📅 Agendar Consulta</a>
                <a routerLink="/paciente/consultas" routerLinkActive="active" class="nav-link">📋 Minhas Consultas</a>
                <a routerLink="/paciente/historico" routerLinkActive="active" class="nav-link">📁 Histórico & Documentos</a>
                <a routerLink="/paciente/autorizacoes" routerLinkActive="active" class="nav-link">🔒 Compartilhamento</a>
                <a routerLink="/paciente/notificacoes" routerLinkActive="active" class="nav-link">📬 Caixa Simulada</a>
              </ng-container>

              <ng-container *ngSwitchCase="'Enfermeiro'">
                <a routerLink="/enfermeiro/fila" routerLinkActive="active" class="nav-link">🩺 Fila de Triagem</a>
              </ng-container>

              <ng-container *ngSwitchCase="'Medico'">
                <a routerLink="/medico/consultas" routerLinkActive="active" class="nav-link">👨‍⚕️ Minhas Consultas</a>
                <a routerLink="/medico/agenda" routerLinkActive="active" class="nav-link">📅 Minha Agenda</a>
              </ng-container>

              <ng-container *ngSwitchCase="'Atendente'">
                <a routerLink="/atendente/dashboard" routerLinkActive="active" class="nav-link">📊 Dashboard Operacional</a>
                <a routerLink="/atendente/agenda" routerLinkActive="active" class="nav-link">📅 Gestão de Agenda</a>
                <a routerLink="/atendente/pacientes" routerLinkActive="active" class="nav-link">👥 Pacientes</a>
              </ng-container>

              <ng-container *ngSwitchCase="'Admin'">
                <a routerLink="/admin/usuarios" routerLinkActive="active" class="nav-link">⚙️ Gerenciar Usuários</a>
                <a routerLink="/admin/saude" routerLinkActive="active" class="nav-link">🏥 Saúde dos Serviços</a>
              </ng-container>
            </ng-container>
          </div>
        </div>

        <div class="nav-right">
          <div class="user-info">
            <span class="badge" [ngClass]="getRoleBadgeClass(user.perfil)">
              {{ user.perfil }}
            </span>
            <span class="user-name">{{ user.nome }}</span>
            <span class="inst-badge" *ngIf="user.instituicaoNome">
              🏢 {{ user.instituicaoNome }}
            </span>
          </div>
          <button class="btn btn-secondary btn-sm" (click)="logout()">Sair</button>
        </div>
      </div>
    </nav>
  `,
  styles: [`
    .navbar {
      background: #ffffff;
      border-bottom: 1px solid var(--border);
      position: sticky;
      top: 0;
      z-index: 50;
      box-shadow: 0 1px 2px rgba(0,0,0,0.03);
    }
    .nav-container {
      max-width: 1300px;
      margin: 0 auto;
      padding: 0.75rem 1.5rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .nav-left, .nav-right {
      display: flex;
      align-items: center;
      gap: 1.5rem;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-weight: 700;
      font-size: 1.125rem;
      color: var(--secondary);
    }
    .nav-links {
      display: flex;
      gap: 0.75rem;
    }
    .nav-link {
      color: var(--text-muted);
      text-decoration: none;
      font-size: 0.875rem;
      font-weight: 500;
      padding: 0.375rem 0.75rem;
      border-radius: 6px;
      transition: all 0.15s ease;
    }
    .nav-link:hover {
      color: var(--primary);
      background: var(--primary-light);
    }
    .nav-link.active {
      color: var(--primary-dark);
      background: var(--primary-light);
      font-weight: 600;
    }
    .user-info {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.875rem;
    }
    .user-name {
      font-weight: 600;
    }
    .inst-badge {
      font-size: 0.75rem;
      background: #f1f5f9;
      color: #475569;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
    }
  `]
})
export class NavbarComponent {
  auth = inject(AuthService);

  logout() {
    this.auth.logout();
  }

  getRoleBadgeClass(perfil: string): string {
    switch (perfil) {
      case 'Paciente': return 'badge-primary';
      case 'Medico': return 'badge-success';
      case 'Enfermeiro': return 'badge-info';
      case 'Atendente': return 'badge-warning';
      case 'Admin': return 'badge-danger';
      default: return 'badge-secondary';
    }
  }
}
