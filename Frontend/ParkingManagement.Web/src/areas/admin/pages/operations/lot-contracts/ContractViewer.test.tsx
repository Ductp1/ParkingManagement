import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AdminDialog } from '../../../components/AdminDialog';
import { ContractViewer } from './ContractViewer';

describe('ContractViewer', () => {
  afterEach(cleanup);

  it('shows the contract in a frame that cannot run scripts and prints it', () => {
    render(<ContractViewer title="Contract preview" html="<p>Điều 1</p>" onClose={() => undefined} />);
    const frame = screen.getByTitle('Contract preview') as HTMLIFrameElement;
    const print = vi.fn();
    Object.defineProperty(frame, 'contentWindow', { value: { focus: vi.fn(), print } });

    fireEvent.click(screen.getByText('Save as PDF'));

    expect(frame.getAttribute('sandbox')).toBe('allow-same-origin allow-modals');
    expect(frame.getAttribute('srcdoc')).toBe('<p>Điều 1</p>');
    expect(print).toHaveBeenCalledTimes(1);
  });

  it('closes on Escape without closing the dialog underneath', () => {
    const closeViewer = vi.fn();
    const closeDialog = vi.fn();
    render(
      <AdminDialog open title="Create lot contract" onClose={closeDialog}>
        <ContractViewer title="Contract preview" html="<p>x</p>" onClose={closeViewer} />
      </AdminDialog>,
    );

    fireEvent.keyDown(screen.getByText('Save as PDF'), { key: 'Escape' });

    expect(closeViewer).toHaveBeenCalledTimes(1);
    expect(closeDialog).not.toHaveBeenCalled();
  });
});
