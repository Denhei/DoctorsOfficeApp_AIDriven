import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService, Consulta } from '../../../services/api.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-consultas-paciente',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="container">
      <div class="page-header flex justify-between items-center">
        <div>
          <h2>📋 Minhas Consultas</h2>
          <p class="text-muted">Acompanhe suas consultas agendadas, em atendimento e histórico concluído.</p>
        </div>
        <a routerLink="/paciente/agenda" class="btn btn-primary">+ Novo Agendamento</a>
      </div>

      <div *ngIf="loading" class="text-muted">Carregando consultas...</div>

      <div *ngIf="!loading && consultas.length === 0" class="card empty-card">
        <p>Você ainda não possui consultas cadastradas.</p>
      </div>

      <div class="consultas-grid" *ngIf="!loading && consultas.length > 0">
        <div *ngFor="let c of consultas" class="consulta-card card">
          <div class="consulta-top flex justify-between items-center">
            <span class="badge" [ngClass]="getStatusBadge(c.status)">{{ c.status }}</span>
            <small class="text-muted">{{ c.inicioUtc | date:'dd/MM/yyyy HH:mm' }}</small>
          </div>

          <div class="consulta-body">
            <h3>{{ c.medicoNome }}</h3>
            <p class="text-muted">{{ c.especialidade }} • 🏢 {{ c.instituicaoNome }}</p>

            <div class="resumo-box" *ngIf="c.resumoValidado">
              <strong>Resumo da Triagem:</strong>
              <p>{{ c.resumoValidado }}</p>
            </div>
          </div>

          <div class="consulta-actions flex justify-between items-center">
            <button 
              *ngIf="c.status === 'Agendada'" 
              class="btn btn-secondary btn-sm"
              (click)="cancelarConsulta(c.id)">
              Cancelar Consulta
            </button>

            <a 
              *ngIf="c.status === 'Concluida' && !c.temPesquisaRespondida" 
              [routerLink]="['/paciente/pesquisa', c.id]" 
              class="btn btn-primary btn-sm">
              ⭐ Avaliar Atendimento
            </a>

            <span *ngIf="c.status === 'Concluida' && c.temPesquisaRespondida" class="badge badge-success">
              ✓ Avaliação Enviada
            </span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .consultas-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 1rem; }
    .consulta-card { display: flex; flex-direction: column; justify-content: space-between; gap: 1rem; }
    .consulta-body h3 { font-size: 1.05rem; margin-bottom: 0.25rem; }
    .resumo-box {
      background: #f8fafc;
      padding: 0.625rem;
      border-radius: 6px;
      font-size: 0.8125rem;
      margin-top: 0.75rem;
    }
    .empty-card { text-align: center; color: var(--text-muted); padding: 2rem; }
  `]
})
export class ConsultasPacienteComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  consultas: Consulta[] = [];
  loading = false;

  ngOnInit() {
    this.carregarConsultas();
  }

  carregarConsultas() {
    this.loading = true;
    this.api.getConsultas().subscribe({
      next: (res) => {
        this.consultas = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  cancelarConsulta(id: string) {
    if (!confirm('Deseja realmente cancelar esta consulta? O horário será liberado na agenda.')) return;
    this.api.atualizarStatusConsulta(id, 'Cancelada').subscribe(() => {
      this.carregarConsultas();
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
