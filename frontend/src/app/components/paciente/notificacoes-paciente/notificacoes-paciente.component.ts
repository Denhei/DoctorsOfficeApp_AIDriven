import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, Notificacao } from '../../../services/api.service';

@Component({
  selector: 'app-notificacoes-paciente',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="container">
      <div class="page-header">
        <h2>📬 Central de Comunicação Simulada</h2>
        <p class="text-muted">Mensagens e convites enviados para o paciente via canais simulados de WhatsApp e E-mail.</p>
      </div>

      <div class="alert alert-warning">
        🔔 <strong>Ambiente de Demonstração:</strong> Os canais abaixo são simulados internamente na aplicação para fins acadêmicos, sem dependência de APIs externas ou operadoras.
      </div>

      <!-- Abas WhatsApp / E-mail -->
      <div class="tabs flex gap-2">
        <button class="btn" [ngClass]="abaAtiva === 'Todos' ? 'btn-primary' : 'btn-secondary'" (click)="abaAtiva = 'Todos'">
          Todas as Mensagens
        </button>
        <button class="btn" [ngClass]="abaAtiva === 'WhatsApp' ? 'btn-primary' : 'btn-secondary'" (click)="abaAtiva = 'WhatsApp'">
          💬 WhatsApp Simulado ({{ contarCanal('WhatsApp') }})
        </button>
        <button class="btn" [ngClass]="abaAtiva === 'Email' ? 'btn-primary' : 'btn-secondary'" (click)="abaAtiva = 'Email'">
          ✉️ E-mail Simulado ({{ contarCanal('Email') }})
        </button>
      </div>

      <div *ngIf="loading" class="text-muted" style="margin-top: 1.5rem;">Carregando mensagens...</div>

      <div *ngIf="!loading && getNotificacoesFiltradas().length === 0" class="card empty-card" style="margin-top: 1.5rem;">
        <p>Nenhuma mensagem recebida nesta caixa de entrada.</p>
      </div>

      <div class="notifs-list" *ngIf="!loading && getNotificacoesFiltradas().length > 0">
        <div *ngFor="let n of getNotificacoesFiltradas()" class="card notif-card flex justify-between items-center">
          <div class="notif-left flex items-center gap-4">
            <span class="canal-badge" [ngClass]="n.canal === 'WhatsApp' ? 'zap' : 'mail'">
              {{ n.canal === 'WhatsApp' ? '💬 WhatsApp' : '✉️ E-mail' }}
            </span>
            <div>
              <strong>{{ n.titulo }}</strong>
              <p class="notif-text">{{ n.texto }}</p>
              <small class="text-muted">{{ n.criadaEmUtc | date:'dd/MM/yyyy HH:mm' }}</small>
            </div>
          </div>

          <div class="notif-right" *ngIf="n.consultaId && n.finalidade === 'ConvitePesquisa'">
            <a [routerLink]="['/paciente/pesquisa', n.consultaId]" class="btn btn-primary btn-sm">
              ⭐ Abrir Formulário de Pesquisa
            </a>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .tabs { margin-bottom: 1.5rem; }
    .notifs-list { display: flex; flex-direction: column; gap: 0.875rem; margin-top: 1.5rem; }
    .notif-card { background: white; }
    .notif-text { font-size: 0.875rem; color: #334155; margin: 0.25rem 0; }
    .canal-badge {
      font-size: 0.75rem;
      font-weight: 700;
      padding: 0.25rem 0.625rem;
      border-radius: 6px;
      white-space: nowrap;
    }
    .canal-badge.zap { background: #dcfce7; color: #166534; }
    .canal-badge.mail { background: #e0f2fe; color: #0369a1; }
    .empty-card { text-align: center; color: var(--text-muted); padding: 2rem; }
  `]
})
export class NotificacoesPacienteComponent implements OnInit {
  private api = inject(ApiService);

  notificacoes: Notificacao[] = [];
  abaAtiva: 'Todos' | 'WhatsApp' | 'Email' = 'Todos';
  loading = false;

  ngOnInit() {
    this.carregarNotificacoes();
  }

  carregarNotificacoes() {
    this.loading = true;
    this.api.getNotificacoes().subscribe({
      next: (res) => {
        this.notificacoes = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  getNotificacoesFiltradas(): Notificacao[] {
    if (this.abaAtiva === 'Todos') return this.notificacoes;
    return this.notificacoes.filter(n => n.canal === this.abaAtiva);
  }

  contarCanal(canal: string): number {
    return this.notificacoes.filter(n => n.canal === canal).length;
  }
}
