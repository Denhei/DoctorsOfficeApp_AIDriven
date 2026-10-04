import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService, Consulta, PesquisaSatisfacao } from '../../../services/api.service';

@Component({
  selector: 'app-pesquisa-satisfacao',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <div class="card form-card">
        <div class="header">
          <div class="icon">⭐</div>
          <h2>Pesquisa de Satisfação do Atendimento</h2>
          <p class="text-muted">Ajude-nos a melhorar a qualidade dos nossos serviços respondendo às perguntas abaixo.</p>
        </div>

        <div *ngIf="pesquisaExistente" class="alert alert-success">
          ✅ <strong>Você já respondeu a esta pesquisa!</strong><br />
          Nota: <strong>{{ pesquisaExistente.nota }} / 5</strong> • Percepção de saúde: <strong>{{ pesquisaExistente.percepcao }}</strong><br />
          <em *ngIf="pesquisaExistente.comentario">"{{ pesquisaExistente.comentario }}"</em>
        </div>

        <div *ngIf="!pesquisaExistente" class="form-body">
          <div *ngIf="consulta" class="consulta-summary">
            <strong>Consulta:</strong> {{ consulta.medicoNome }} ({{ consulta.especialidade }}) • 🏢 {{ consulta.instituicaoNome }}
          </div>

          <!-- Pergunta 1: Nota 1 a 5 -->
          <div class="form-group">
            <label class="form-label">1. Como você avalia a qualidade geral do seu atendimento? (1 a 5 estrelas)</label>
            <div class="stars-rating flex gap-2">
              <button 
                *ngFor="let n of [1, 2, 3, 4, 5]" 
                type="button" 
                class="star-btn" 
                [ngClass]="{'active': nota >= n}"
                (click)="nota = n">
                ★
              </button>
            </div>
            <small class="text-muted">Nota selecionada: {{ nota }} de 5</small>
          </div>

          <!-- Pergunta 2: Percepção pós-atendimento -->
          <div class="form-group">
            <label class="form-label">2. Como você percebe seu estado de saúde após o atendimento recebido?</label>
            <div class="radio-options flex flex-col gap-2">
              <label class="radio-label">
                <input type="radio" [(ngModel)]="percepcao" value="Melhor" name="percepcao" />
                Sinto-me melhor / orientado
              </label>
              <label class="radio-label">
                <input type="radio" [(ngModel)]="percepcao" value="Igual" name="percepcao" />
                Sinto-me igual
              </label>
              <label class="radio-label">
                <input type="radio" [(ngModel)]="percepcao" value="Pior" name="percepcao" />
                Sinto-me pior
              </label>
              <label class="radio-label">
                <input type="radio" [(ngModel)]="percepcao" value="NaoInformado" name="percepcao" />
                Prefiro não responder
              </label>
            </div>
          </div>

          <!-- Pergunta 3: Comentário opcional -->
          <div class="form-group">
            <label class="form-label">3. Comentários ou sugestões adicionais (opcional):</label>
            <textarea class="form-control" rows="3" [(ngModel)]="comentario" placeholder="Deixe sua mensagem..."></textarea>
          </div>

          <div class="flex justify-between items-center">
            <button class="btn btn-secondary" (click)="voltar()">Voltar</button>
            <button class="btn btn-primary" (click)="enviarPesquisa()" [disabled]="enviando || nota < 1">
              {{ enviando ? 'Enviando...' : 'Submeter Avaliação' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .form-card { max-width: 600px; margin: 2rem auto; }
    .header { text-align: center; margin-bottom: 1.5rem; }
    .icon { font-size: 2.5rem; margin-bottom: 0.5rem; }
    .consulta-summary { background: #f8fafc; padding: 0.75rem; border-radius: 8px; margin-bottom: 1.25rem; font-size: 0.875rem; }
    .stars-rating { margin: 0.5rem 0; }
    .star-btn {
      background: none;
      border: none;
      font-size: 2rem;
      color: #cbd5e1;
      cursor: pointer;
      transition: color 0.15s ease;
    }
    .star-btn.active { color: #f59e0b; }
    .radio-label { display: flex; align-items: center; gap: 0.5rem; font-size: 0.875rem; cursor: pointer; }
  `]
})
export class PesquisaSatisfacaoComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  consultaId = '';
  consulta: Consulta | null = null;
  pesquisaExistente: PesquisaSatisfacao | null = null;

  nota = 5;
  percepcao = 'Melhor';
  comentario = '';
  enviando = false;

  ngOnInit() {
    this.consultaId = this.route.snapshot.paramMap.get('id') || '';
    if (this.consultaId) {
      this.api.getConsulta(this.consultaId).subscribe(res => this.consulta = res);
      this.api.getPesquisa(this.consultaId).subscribe({
        next: (res) => this.pesquisaExistente = res,
        error: () => this.pesquisaExistente = null
      });
    }
  }

  enviarPesquisa() {
    if (!this.consultaId) return;
    this.enviando = true;
    this.api.responderPesquisa(this.consultaId, {
      nota: this.nota,
      percepcao: this.percepcao,
      comentario: this.comentario
    }).subscribe({
      next: (res) => {
        this.enviando = false;
        this.pesquisaExistente = res;
      },
      error: (err) => {
        this.enviando = false;
        alert(err.error?.mensagem || 'Erro ao submeter pesquisa.');
      }
    });
  }

  voltar() {
    this.router.navigate(['/paciente/consultas']);
  }
}
