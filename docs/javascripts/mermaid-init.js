(function () {
    var MIN_ZOOM = 0.5;
    var MAX_ZOOM = 3;
    var ZOOM_STEP = 1.15;

    function effectiveTheme() {
        var scheme = document.body.getAttribute('data-md-color-scheme');
        return scheme === 'slate' ? 'dark' : 'neutral';
    }

    function normalizeLegacyNodes() {
        document.querySelectorAll('pre.mermaid').forEach(function (pre) {
            var code = pre.querySelector('code');
            var div = document.createElement('div');
            div.className = 'mermaid';
            div.textContent = code ? code.textContent : pre.textContent;
            pre.replaceWith(div);
        });
    }

    function toolbarButton(label, title, action) {
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.title = title;
        btn.setAttribute('aria-label', title);
        btn.dataset.action = action;
        btn.textContent = label;
        return btn;
    }

    function wrapMermaidNode(node) {
        if (node.closest('.cm-mermaid-block')) {
            return node.closest('.cm-mermaid-block');
        }

        var source = node.textContent.trim();
        var parent = node.parentNode;

        var block = document.createElement('div');
        block.className = 'cm-mermaid-block';
        block.tabIndex = 0;
        block.dataset.mermaidSource = source;

        var toolbar = document.createElement('div');
        toolbar.className = 'cm-mermaid-toolbar';
        toolbar.appendChild(toolbarButton('+', 'Zoom in', 'zoom-in'));
        toolbar.appendChild(toolbarButton('−', 'Zoom out', 'zoom-out'));
        toolbar.appendChild(toolbarButton('↺', 'Reset view', 'reset'));
        toolbar.appendChild(toolbarButton('⛶', 'Fullscreen', 'fullscreen'));

        var viewport = document.createElement('div');
        viewport.className = 'cm-mermaid-viewport';

        var body = document.createElement('div');
        body.className = 'cm-mermaid-body';

        var handle = document.createElement('div');
        handle.className = 'cm-mermaid-resize-handle';
        handle.title = 'Drag to resize';

        viewport.appendChild(body);
        block.appendChild(toolbar);
        block.appendChild(viewport);
        block.appendChild(handle);

        parent.replaceChild(block, node);
        body.appendChild(node);

        return block;
    }

    function fixSvg(svg) {
        svg.removeAttribute('width');
        svg.removeAttribute('height');
        svg.style.display = 'block';
        svg.setAttribute('preserveAspectRatio', 'xMidYMid meet');
    }

    function measureBaseSvgWidth(block) {
        var svg = block.querySelector('svg');
        if (!svg) {
            return 0;
        }
        return svg.getBoundingClientRect().width / (block._zoom || 1);
    }

    function applyZoom(block) {
        var svg = block.querySelector('svg');
        var body = block.querySelector('.cm-mermaid-body');
        if (!svg || !body) {
            return;
        }

        if (!block._baseSvgWidth) {
            var measured = measureBaseSvgWidth(block);
            if (measured > 0) {
                block._baseSvgWidth = measured;
            }
        }
        if (!block._baseSvgWidth) {
            return;
        }

        var px = block._baseSvgWidth * block._zoom;
        svg.style.width = px + 'px';
        svg.style.maxWidth = 'none';
        svg.style.height = 'auto';
        body.style.width = 'max-content';
        body.style.minWidth = '100%';
    }

    function syncViewportHeight(block) {
        var viewport = block.querySelector('.cm-mermaid-viewport');
        var body = block.querySelector('.cm-mermaid-body');
        if (block._userHeight) {
            viewport.style.height = block._userHeight + 'px';
            return;
        }
        var contentH = body.offsetHeight + 24;
        viewport.style.height = Math.min(Math.max(contentH, 120), 720) + 'px';
    }

    function resetView(block) {
        block._zoom = 1;
        block._baseSvgWidth = 0;
        block._userHeight = 0;
        var svg = block.querySelector('svg');
        if (svg) {
            fixSvg(svg);
        }
        applyZoom(block);
        var viewport = block.querySelector('.cm-mermaid-viewport');
        viewport.scrollLeft = 0;
        viewport.scrollTop = 0;
        syncViewportHeight(block);
    }

    function installInteractions(block) {
        if (block.dataset.cmEnhanced === '1') {
            return;
        }
        block.dataset.cmEnhanced = '1';

        block._zoom = 1;
        block._baseSvgWidth = 0;
        block._userHeight = 0;

        var svg = block.querySelector('svg');
        if (svg) {
            fixSvg(svg);
        }

        requestAnimationFrame(function () {
            block._baseSvgWidth = measureBaseSvgWidth(block);
            applyZoom(block);
            syncViewportHeight(block);
        });

        var viewport = block.querySelector('.cm-mermaid-viewport');

        block.querySelector('.cm-mermaid-toolbar').addEventListener('click', function (e) {
            var btn = e.target.closest('button[data-action]');
            if (!btn) {
                return;
            }
            if (btn.dataset.action === 'zoom-in') {
                block._zoom = Math.min(MAX_ZOOM, block._zoom * ZOOM_STEP);
                applyZoom(block);
            } else if (btn.dataset.action === 'zoom-out') {
                block._zoom = Math.max(MIN_ZOOM, block._zoom / ZOOM_STEP);
                applyZoom(block);
            } else if (btn.dataset.action === 'reset') {
                resetView(block);
            } else if (btn.dataset.action === 'fullscreen') {
                toggleFullscreen(block);
            }
        });

        var panning = false;
        var startX = 0;
        var startY = 0;
        var scrollLeft = 0;
        var scrollTop = 0;

        viewport.addEventListener('pointerdown', function (e) {
            if (e.target.closest('.cm-mermaid-toolbar') || e.target.closest('.cm-mermaid-resize-handle')) {
                return;
            }
            panning = true;
            viewport.classList.add('cm-mermaid-panning');
            startX = e.clientX;
            startY = e.clientY;
            scrollLeft = viewport.scrollLeft;
            scrollTop = viewport.scrollTop;
            viewport.setPointerCapture(e.pointerId);
        });

        viewport.addEventListener('pointermove', function (e) {
            if (!panning) {
                return;
            }
            viewport.scrollLeft = scrollLeft - (e.clientX - startX);
            viewport.scrollTop = scrollTop - (e.clientY - startY);
        });

        function endPan(e) {
            if (!panning) {
                return;
            }
            panning = false;
            viewport.classList.remove('cm-mermaid-panning');
            try {
                viewport.releasePointerCapture(e.pointerId);
            } catch (err) {
                /* ponytail: ignore */
            }
        }

        viewport.addEventListener('pointerup', endPan);
        viewport.addEventListener('pointercancel', endPan);

        viewport.addEventListener('wheel', function (e) {
            if (!e.ctrlKey && !e.metaKey) {
                return;
            }
            e.preventDefault();
            var factor = e.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP;
            block._zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, block._zoom * factor));
            applyZoom(block);
        }, { passive: false });

        var resizing = false;
        var resizeStartY = 0;
        var resizeStartH = 0;
        var handle = block.querySelector('.cm-mermaid-resize-handle');

        handle.addEventListener('pointerdown', function (e) {
            e.preventDefault();
            resizing = true;
            resizeStartY = e.clientY;
            resizeStartH = viewport.getBoundingClientRect().height;
            handle.setPointerCapture(e.pointerId);
        });

        handle.addEventListener('pointermove', function (e) {
            if (!resizing) {
                return;
            }
            block._userHeight = Math.min(
                window.innerHeight * 0.85,
                Math.max(120, resizeStartH + (e.clientY - resizeStartY))
            );
            viewport.style.height = block._userHeight + 'px';
        });

        function endResize(e) {
            if (!resizing) {
                return;
            }
            resizing = false;
            try {
                handle.releasePointerCapture(e.pointerId);
            } catch (err) {
                /* ponytail: ignore */
            }
        }

        handle.addEventListener('pointerup', endResize);
        handle.addEventListener('pointercancel', endResize);
    }

    var backdrop = null;

    function toggleFullscreen(block) {
        if (block.classList.contains('cm-mermaid-fullscreen')) {
            block.classList.remove('cm-mermaid-fullscreen');
            if (backdrop) {
                backdrop.remove();
                backdrop = null;
            }
            document.body.style.overflow = '';
            syncViewportHeight(block);
            return;
        }

        backdrop = document.createElement('div');
        backdrop.className = 'cm-mermaid-backdrop';
        backdrop.addEventListener('click', function () {
            toggleFullscreen(block);
        });
        document.body.appendChild(backdrop);
        document.body.style.overflow = 'hidden';
        block.classList.add('cm-mermaid-fullscreen');
        block._userHeight = Math.max(window.innerHeight - 48, 200);
        block.querySelector('.cm-mermaid-viewport').style.height = block._userHeight + 'px';
    }

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') {
            return;
        }
        var open = document.querySelector('.cm-mermaid-fullscreen');
        if (open) {
            toggleFullscreen(open);
        }
    });

    function prepareBlocks() {
        normalizeLegacyNodes();
        document.querySelectorAll('.md-typeset .mermaid').forEach(function (node) {
            if (!node.closest('.cm-mermaid-body')) {
                wrapMermaidNode(node);
            }
        });
    }

    function enhanceRendered() {
        requestAnimationFrame(function () {
            document.querySelectorAll('.cm-mermaid-block').forEach(installInteractions);
        });
    }

    function renderAll() {
        if (typeof mermaid === 'undefined') {
            return false;
        }

        prepareBlocks();
        mermaid.initialize({
            startOnLoad: false,
            theme: effectiveTheme(),
            securityLevel: 'loose'
        });

        return mermaid.run({ querySelector: '.cm-mermaid-body .mermaid' }).then(function () {
            enhanceRendered();
        });
    }

    function rerenderAll() {
        document.querySelectorAll('.cm-mermaid-block').forEach(function (block) {
            var body = block.querySelector('.cm-mermaid-body');
            var source = block.dataset.mermaidSource;
            if (!body || !source) {
                return;
            }
            body.innerHTML = '';
            var node = document.createElement('div');
            node.className = 'mermaid';
            node.textContent = source;
            body.appendChild(node);
            block.dataset.cmEnhanced = '0';
            block._zoom = 1;
            block._baseSvgWidth = 0;
            block._userHeight = 0;
        });

        mermaid.initialize({
            startOnLoad: false,
            theme: effectiveTheme(),
            securityLevel: 'loose'
        });

        return mermaid.run({ querySelector: '.cm-mermaid-body .mermaid' }).then(function () {
            enhanceRendered();
        });
    }

    function waitForMermaid(attempts) {
        var result = renderAll();
        if (result && typeof result.then === 'function') {
            result.catch(function (error) {
                console.error('Mermaid render failed', error);
            });
            return;
        }

        if (typeof mermaid !== 'undefined') {
            return;
        }

        if (attempts < 200) {
            setTimeout(function () {
                waitForMermaid(attempts + 1);
            }, 25);
        }
    }

    // ponytail: Material toggles data-md-color-scheme on <body>; re-render Mermaid on palette change.
    var schemeObserver = new MutationObserver(function () {
        if (typeof mermaid === 'undefined') {
            return;
        }
        rerenderAll().catch(function (error) {
            console.error('Mermaid re-render failed', error);
        });
    });
    schemeObserver.observe(document.body, {
        attributes: true,
        attributeFilter: ['data-md-color-scheme']
    });

    // Material instant navigation replaces content without full reload.
    if (typeof document$ !== 'undefined' && document$.subscribe) {
        document$.subscribe(function () {
            waitForMermaid(0);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            waitForMermaid(0);
        });
    } else {
        waitForMermaid(0);
    }
})();
