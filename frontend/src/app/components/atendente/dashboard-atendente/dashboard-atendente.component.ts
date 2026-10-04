import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService, DashboardOperacional, Consulta } from '../../../services/api.service';

@Component({
  selector: 'app-dashboard-atendente',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container">
      <div class="page-header flex justify-between items-center">
        <div>
          <h2>📊 Dashboard Operacional da Recepção</h2>
          <p class="text-muted">Indicadores agregados em tempo real da unidade de atendimento.</p>
        </div>
        <button class="btn btn-secondary btn-sm" (click)="carregarDados()">🔄 Atualizar Indicadores</button>
      </div>

      <!-- Cards de Métricas -->
      <div class="metrics-grid" *ngIf="dashboard">
        <div class="card metric-card">
          <div class="metric-icon">📅</div>
          <div class="metric-info">
            <span class="metric-value">{{ dashboard.totalConsultasAgendadas }}</span>
            <span class="metric-label">Agendadas</span>
          </div>
        </div>

        <div class="card metric-card">
          <div class="metric-icon">🩺</div>
          <div class="metric-info">
            <span class="metric-value">{{ dashboard.totalConsultasEmAtendimento }}</span>
            <span class="metric-label">Em Atendimento</span>
          </div>
        </div>

        <div class="card metric-card">
          <div class="metric-icon">✅</div>
          <div class="metric-info">
            <span class="metric-value">{{ dashboard.totalConsultasConcluidas }}</span>
            <span class="metric-label">Concluídas</span>
          </div>
        </div>

        <div class="card metric-card">
          <div class="metric-icon">⏳</div>
          <div class="metric-info">
            <span class="metric-value">{{ dashboard.totalColetasPendentesValidacao }}</span>
            <span class="metric-label">Coletas Pendentes</span>
          </div>
        </div>

        <div class="card metric-card star-card">
          <div class="metric-icon">⭐</div>
          <div class="metric-info">
            <span class="metric-value">{{ dashboard.mediaSatisfacao | number:'1.1-2' }} / 5.0</span>
            <span class="metric-label">Média de Satisfação ({{ dashboard.totalPesquisasRespondidas }} avaliações)</span>
          </div>
        </div>
      </div>

      <!-- Tabela Operacional de Consultas -->
      <div class="section" style="margin-top: 2rem;">
        <h3>Consultas da Unidade</h3>
        <div *ngIf="loading" class="text-muted">Carregando consultas...</div>

        <div class="card table-card" *ngIf="!loading">
          <table class="data-table">
            <thead>
              <tr>
                <th>Paciente</th>
                <th>Médico / Especialidade</th>
                <th>Horário</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let c of consultas">
                <td><strong>{{ c.pacienteNome }}</strong></td>
                <td>{{ c.medicoNome }} ({{ c.especialidade }})</td>
                <td>{{ c.inicioUtc | date:'dd/MM/yyyy HH:mm' }}</td>
                <td><span class="badge" [ngClass]="getStatusBadge(c.status)">{{ c.status }}</span></td>
              </tr>
              <tr *ngIf="consultas.length === 0">
                <td colspan="4" style="text-align: center; color: var(--text-muted);">Nenhuma consulta registrada.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .metrics-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
      gap: 1rem;
    }
    .metric-card {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1.25rem;
    }
    .metric-icon { font-size: 2rem; }
    .metric-info { display: flex; flex-direction: column; }
    .metric-value { font-size: 1.5rem; font-weight: 700; color: var(--secondary); }
    .metric-label { font-size: 0.75rem; color: var(--text-muted); text-transform: uppercase; font-weight: 600; }
    .star-card { background: linear-gradient(135deg, #fffbeb 0%, #fef3c7 100%); border-color: #fde68a; }
    .table-card { padding: 0; overflow: hidden; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.875rem; }
    .data-table th, .data-table td { padding: 0.875rem 1.25rem; border-bottom: 1px solid var(--border); }
    .data-table th { background: #f8fafc; font-weight: 600; color: #475569; }
  `]
})
export class DashboardAtendenteComponent implements OnInit {
  private api = inject(ApiService);

  dashboard: DashboardOperacional | null = null;
  consultas: Consulta[] = [];
  loading = false;

  ngOnInit() {
    this.carregarDados();
  }

  carregarDados() {
    this.loading = true;
    this.api.getDashboard().subscribe(res => this.dashboard = res);
    this.api.getConsultas().subscribe({
      next: (res) => {
        this.consultas = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  getStatusBadge(status: string): string {
    switch (status) {
      case 'Agendada': return 'badge-primary';
      case 'EmAtendimento': return 'badge-warning';
      case 'Concluida': return 'badge-success';
      case 'Cancelada': return 'badge-danger';
      default: return 'badge-secondary';
    }
  }
}
