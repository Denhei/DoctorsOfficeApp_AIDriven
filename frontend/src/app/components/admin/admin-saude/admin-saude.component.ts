import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService, HealthStatus } from '../../../services/api.service';

@Component({
  selector: 'app-admin-saude',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container">
      <div class="page-header flex justify-between items-center">
        <div>
          <h2>🏥 Diagnóstico & Saúde dos Serviços</h2>
          <p class="text-muted">Monitoramento em tempo real do estado de conexão com PostgreSQL e MongoDB.</p>
        </div>
        <button class="btn btn-secondary btn-sm" (click)="verificarSaude()">🔄 Verificar Agora</button>
      </div>

      <div *ngIf="loading" class="text-muted">Testando serviços...</div>

      <div *ngIf="!loading && saude" class="saude-grid">
        <div class="card service-card">
          <div class="flex justify-between items-center">
            <h3>🐘 PostgreSQL</h3>
            <span class="badge" [ngClass]="saude.servicos.postgreSql === 'Online' ? 'badge-success' : 'badge-danger'">
              {{ saude.servicos.postgreSql }}
            </span>
          </div>
          <p class="text-muted">Banco de dados relacional (EF Core 8 / Npgsql). Armazena usuários, instituições, agendamentos e prontuários.</p>
        </div>

        <div class="card service-card">
          <div class="flex justify-between items-center">
            <h3>🍃 MongoDB</h3>
            <span class="badge" [ngClass]="saude.servicos.mongoDb === 'Online' ? 'badge-success' : 'badge-danger'">
              {{ saude.servicos.mongoDb }}
            </span>
          </div>
          <p class="text-muted">Banco de dados documental. Armazena a coleção 'conversas' com as mensagens do chatbot determinístico e resumos preliminares.</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .saude-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1.5rem; margin-top: 1rem; }
    .service-card { display: flex; flex-direction: column; gap: 0.75rem; }
  `]
})
export class AdminSaudeComponent implements OnInit {
  private api = inject(ApiService);

  saude: HealthStatus | null = null;
  loading = false;

  ngOnInit() {
    this.verificarSaude();
  }

  verificarSaude() {
    this.loading = true;
    this.api.getHealth().subscribe({
      next: (res) => {
        this.saude = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }
}
