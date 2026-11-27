import React from 'react';
import type { ContractPlaceholderDto, TemplateAppendixKind, TemplateArticleDto, TemplateContentDto } from '@/types/adminContractTemplates';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { SelectField, ToggleField } from '../../../components/FormFields';
import { Panel } from '../../../components/Panel';
import { APPENDIX_KINDS, move, SPECIAL_TERMS_LEAD } from './contractTemplateFormat';
import { ListEditor, RowActions, TemplateText } from './TemplateFormFields';

interface Props {
  content: TemplateContentDto;
  onChange: (content: TemplateContentDto) => void;
  catalog: ContractPlaceholderDto[];
  readOnly?: boolean;
}

/**
 * Soạn hợp đồng bằng form (US-116 v11): đầu trang, căn cứ pháp lý, lời dẫn, các điều + khoản, phụ lục, chữ ký.
 * Quốc hiệu, khối Bên A / Bên B, số khoản "n.m.", bảng phụ lục bãi / tài khoản và khối chữ ký do hệ thống dựng.
 */
export const TemplateContentEditor: React.FC<Props> = ({ content, onChange, catalog, readOnly }) => {
  const set = (patch: Partial<TemplateContentDto>) => onChange({ ...content, ...patch });
  const setArticle = (i: number, patch: Partial<TemplateArticleDto>) =>
    set({ articles: content.articles.map((a, j) => (j === i ? { ...a, ...patch } : a)) });
  const markSpecial = (i: number, on: boolean) =>
    set({
      articles: content.articles.map((a, j) =>
        j === i
          ? { ...a, isSpecialTerms: on, clauses: on && a.clauses.length === 0 ? [SPECIAL_TERMS_LEAD] : a.clauses }
          : on
            ? { ...a, isSpecialTerms: false }
            : a
      ),
    });

  return (
    <>
      <Panel title="Header" description="Printed above the parties. National header, contract number and date are added by the system">
        <div className="flex flex-col gap-4">
          <TemplateText label="Contract title" required value={content.header.title} readOnly={readOnly} catalog={catalog} noFields
            onChange={(title) => set({ header: { ...content.header, title } })} placeholder="Hợp đồng hợp tác khai thác bãi đỗ xe" />
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <TemplateText label="Subtitle" value={content.header.subtitle ?? ''} readOnly={readOnly} catalog={catalog} noFields
              onChange={(subtitle) => set({ header: { ...content.header, subtitle } })} placeholder="TRÊN NỀN TẢNG SMART PARKING MARKETPLACE" />
            <TemplateText label="Place of signing" value={content.header.placeOfSigning} readOnly={readOnly} catalog={catalog} noFields
              onChange={(placeOfSigning) => set({ header: { ...content.header, placeOfSigning } })} placeholder="TP. Hồ Chí Minh" />
          </div>
        </div>
      </Panel>

      <Panel title="Legal bases" description="“Căn cứ …” lines, printed in italics before the parties">
        <ListEditor label="Legal bases" items={content.legalBases} onChange={(legalBases) => set({ legalBases })} catalog={catalog}
          readOnly={readOnly} addLabel="Add legal basis" rows={1} placeholder="Căn cứ Bộ luật Dân sự số 91/2015/QH13 ngày 24/11/2015;" />
      </Panel>

      <Panel title="Introduction" description="Printed after the Party A / Party B blocks (filled from the platform and the partner application)">
        <TemplateText label="Introduction" value={content.introduction ?? ''} readOnly={readOnly} catalog={catalog} multiline rows={3}
          onChange={(introduction) => set({ introduction })}
          placeholder="Hai bên thống nhất ký kết Hợp đồng số {{ContractNumber}} để khai thác bãi đỗ xe {{LotName}} … với các điều khoản sau:" />
      </Panel>

      <Panel title="Articles" description="Numbered automatically (ĐIỀU 1, 2… and clauses 1.1, 1.2…). One article prints the special terms of each lot">
        <div className="flex flex-col gap-4">
          {content.articles.length === 0 && <Banner tone="warning">No articles yet.</Banner>}
          {content.articles.map((article, i) => (
            <section key={i} className="flex flex-col gap-3 border border-(--admin-border-default) p-3">
              <div className="flex items-start gap-2">
                <span className="adm-type-body-strong w-16 shrink-0 pt-7 text-(--admin-text-secondary)">{`Điều ${i + 1}`}</span>
                <TemplateText label="Article title" required value={article.title} readOnly={readOnly} catalog={catalog} noFields
                  onChange={(title) => setArticle(i, { title })} placeholder="PHẠM VI HỢP TÁC" />
                <div className="pt-6">
                  <RowActions index={i} count={content.articles.length} readOnly={readOnly} removeLabel="Remove article"
                    onMove={(f, t) => set({ articles: move(content.articles, f, t) })}
                    onRemove={(j) => set({ articles: content.articles.filter((_, k) => k !== j) })} />
                </div>
              </div>
              <ToggleField label="Special terms article – prints the terms typed for each lot (Create / Revise lot contract)"
                checked={article.isSpecialTerms} disabled={readOnly} onChange={(on) => markSpecial(i, on)} />
              {article.isSpecialTerms ? (
                <ListEditor label="Lead text" items={article.clauses} onChange={(clauses) => setArticle(i, { clauses })} catalog={catalog}
                  readOnly={readOnly} addLabel="Add paragraph" rows={2}
                  helper="Printed before the lot's special terms. If a lot has none, the article reads “Không có thỏa thuận riêng.”" />
              ) : (
                <ListEditor label="Clauses" items={article.clauses} onChange={(clauses) => setArticle(i, { clauses })} catalog={catalog}
                  readOnly={readOnly} addLabel="Add clause" rows={2} prefix={(j) => `${i + 1}.${j + 1}.`}
                  helper="One clause per box; new lines inside a box become sub-paragraphs (a), b)…)." />
              )}
            </section>
          ))}
          {!readOnly && (
            <div>
              <AdminButton variant="secondary" onClick={() => set({ articles: [...content.articles, { title: '', clauses: [''], isSpecialTerms: false }] })}>
                Add article
              </AdminButton>
            </div>
          )}
        </div>
      </Panel>

      <Panel title="Appendices" description="Lot and payout-account tables are filled by the system; add notes below each appendix">
        <div className="flex flex-col gap-4">
          {content.appendices.map((appendix, i) => (
            <section key={i} className="flex flex-col gap-3 border border-(--admin-border-default) p-3">
              <div className="flex items-start gap-2">
                <div className="grid min-w-0 flex-1 grid-cols-1 gap-3 md:grid-cols-3">
                  <TemplateText label="Title" required value={appendix.title} readOnly={readOnly} catalog={catalog} noFields
                    onChange={(title) => set({ appendices: content.appendices.map((a, j) => (j === i ? { ...a, title } : a)) })} placeholder="Phụ lục 01" />
                  <TemplateText label="Subtitle" value={appendix.subtitle ?? ''} readOnly={readOnly} catalog={catalog} noFields
                    onChange={(subtitle) => set({ appendices: content.appendices.map((a, j) => (j === i ? { ...a, subtitle } : a)) })} />
                  <SelectField label="Content" value={appendix.kind} disabled={readOnly} options={APPENDIX_KINDS}
                    onChange={(kind) =>
                      set({ appendices: content.appendices.map((a, j) => (j === i ? { ...a, kind: kind as TemplateAppendixKind } : a)) })} />
                </div>
                <div className="pt-6">
                  <RowActions index={i} count={content.appendices.length} readOnly={readOnly} removeLabel="Remove appendix"
                    onMove={(f, t) => set({ appendices: move(content.appendices, f, t) })}
                    onRemove={(j) => set({ appendices: content.appendices.filter((_, k) => k !== j) })} />
                </div>
              </div>
              <ListEditor label="Notes" items={appendix.notes} catalog={catalog} readOnly={readOnly} addLabel="Add note" rows={2}
                onChange={(notes) => set({ appendices: content.appendices.map((a, j) => (j === i ? { ...a, notes } : a)) })} />
            </section>
          ))}
          {!readOnly && (
            <div>
              <AdminButton variant="secondary"
                onClick={() => set({ appendices: [...content.appendices, { kind: 'Text', title: `Phụ lục ${String(content.appendices.length + 1).padStart(2, '0')}`, subtitle: null, notes: [] }] })}>
                Add appendix
              </AdminButton>
            </div>
          )}
        </div>
      </Panel>

      <Panel title="Signature & closing" description="Signature block shows Party A's position / representative and Party B's legal representative">
        <div className="flex flex-col gap-4">
          <TemplateText label="Signature note" value={content.signNote ?? ''} readOnly={readOnly} catalog={catalog} noFields
            onChange={(signNote) => set({ signNote })} placeholder="(Ký, ghi rõ họ tên, đóng dấu)" />
          <TemplateText label="Closing note" value={content.footnote ?? ''} readOnly={readOnly} catalog={catalog} multiline rows={2}
            onChange={(footnote) => set({ footnote })} placeholder="Printed in italics at the end (optional)" />
        </div>
      </Panel>
    </>
  );
};
