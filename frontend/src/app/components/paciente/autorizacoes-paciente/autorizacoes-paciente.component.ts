import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, Autorizacao, Instituicao } from '../../../services/api.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-autorizacoes-paciente',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <div class="page-header flex justify-between items-center">
        <div>
          <h2>🔒 Compartilhamento de Dados Interinstitucional</h2>
          <p class="text-muted">Você tem o controle total: conceda ou revogue a autorização para que médicos de outra instituição acessem seu histórico.</p>
        </div>
      </div>

      <div class="alert alert-info">
        🛡️ <strong>Privacidade Garantida:</strong> Por padrão, os médicos da Clínica B não têm acesso aos seus atendimentos realizados na Clínica A, a menos que você conceda autorização explícita abaixo. Ao revogar, novos acessos são imediatamente bloqueados.
      </div>

      <!-- Conceder Nova Autorização -->
      <div class="card form-card">
        <h3>Conceder Nova Autorização</h3>
        <div class="grid grid-cols-2 gap-4" style="margin-top: 1rem;">
          <div class="form-group">
            <label class="form-label">Instituição de Origem (Onde estão seus dados):</label>
            <select class="form-control" [(ngModel)]="origemId">
              <option *ngFor="let inst of instituicoes" [value]="inst.id">{{ inst.nome }}</option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">Instituição de Destino (Quem poderá acessar):</label>
            <select class="form-control" [(ngModel)]="destinoId">
              <option *ngFor="let inst of instituicoes" [value]="inst.id">{{ inst.nome }}</option>
            </select>
          </div>
        </div>

        <button class="btn btn-primary" (click)="conceder()" [disabled]="origemId === destinoId || salvando">
          {{ salvando ? 'Autorizando...' : 'Conceder Autorização' }}
        </button>
      </div>

      <!-- Lista de Autorizações Ativas -->
      <div class="section" style="margin-top: 2rem;">
        <h3>Autorizações Registradas</h3>

        <div *ngIf="loading" class="text-muted">Carregando autorizações...</div>

        <div *ngIf="!loading && autorizacoes.length === 0" class="card empty-card">
          <p>Nenhuma autorização concedida até o momento.</p>
        </div>

        <div class="auts-grid" *ngIf="!loading && autorizacoes.length > 0">
          <div *ngFor="let aut of autorizacoes" class="card aut-card flex justify-between items-center" [ngClass]="{'revogada': !aut.ativa}">
            <div>
              <div class="flex items-center gap-2">
                <span class="badge" [ngClass]="aut.ativa ? 'badge-success' : 'badge-danger'">
                  {{ aut.ativa ? 'Ativa' : 'Revogada' }}
                </span>
                <strong>{{ aut.instituicaoOrigemNome }} ➔ {{ aut.instituicaoDestinoNome }}</strong>
              </div>
              <small class="text-muted">
                Concedida em: {{ aut.criadaEmUtc | date:'dd/MM/yyyy HH:mm' }}
                <span *ngIf="aut.revogadaEmUtc">• Revogada em: {{ aut.revogadaEmUtc | date:'dd/MM/yyyy HH:mm' }}</span>
              </small>
            </div>

            <button *ngIf="aut.ativa" class="btn btn-danger btn-sm" (click)="revogar(aut.id)">
              Revogar Acesso
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .form-card { margin-bottom: 1.5rem; }
    .auts-grid { display: flex; flex-direction: column; gap: 0.75rem; }
    .aut-card.revogada { opacity: 0.6; background: #f8fafc; }
    .empty-card { text-align: center; color: var(--text-muted); padding: 2rem; }
  `]
})
export class AutorizacoesPacienteComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  instituicoes: Instituicao[] = [];
  autorizacoes: Autorizacao[] = [];
  origemId = '';
  destinoId = '';
  loading = false;
  salvando = false;

  ngOnInit() {
    this.api.getInstituicoes().subscribe(res => {
      this.instituicoes = res;
      if (res.length >= 2) {
        this.origemId = res[0].id;
        this.destinoId = res[1].id;
      }
    });
    this.carregarAutorizacoes();
  }

  carregarAutorizacoes() {
    const user = this.auth.currentUser();
    if (!user?.pacienteId) return;

    this.loading = true;
    this.api.getAutorizacoes(user.pacienteId).subscribe({
      next: (res) => {
        this.autorizacoes = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  conceder() {
    const user = this.auth.currentUser();
    if (!user?.pacienteId || !this.origemId || !this.destinoId) return;

    this.salvando = true;
    this.api.concederAutorizacao(user.pacienteId, this.origemId, this.destinoId).subscribe({
      next: () => {
        this.salvando = false;
        this.carregarAutorizacoes();
      },
      error: (err) => {
        this.salvando = false;
        alert(err.error?.mensagem || 'Erro ao conceder autorização.');
      }
    });
  }

  revogar(id: string) {
    if (!confirm('Tem certeza que deseja revogar o compartilhamento com esta instituição?')) return;
    this.api.revogarAutorizacao(id).subscribe(() => {
      this.carregarAutorizacoes();
    });
  }
}
