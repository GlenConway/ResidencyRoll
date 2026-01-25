/**
 * 2026 Liquid Glass - Responsive Lighting System
 * Tracks user focus points and creates dynamic lighting effects
 * Spatial Computing Ready
 */

(function() {
    'use strict';

    // Check for reduced motion preference
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    
    if (prefersReducedMotion) {
        console.log('Liquid Glass: Animations disabled due to user preference');
        return;
    }

    let mouseX = 50;
    let mouseY = 50;
    let targetX = 50;
    let targetY = 50;
    let animationFrame;

    /**
     * Smooth interpolation for natural movement
     */
    function lerp(start, end, factor) {
        return start + (end - start) * factor;
    }

    /**
     * Update CSS custom properties for dynamic lighting
     */
    function updateLighting() {
        targetX = lerp(targetX, mouseX, 0.1);
        targetY = lerp(targetY, mouseY, 0.1);

        document.body.style.setProperty('--mouse-x', `${targetX}%`);
        document.body.style.setProperty('--mouse-y', `${targetY}%`);

        // Continue animation if values haven't converged
        if (Math.abs(targetX - mouseX) > 0.1 || Math.abs(targetY - mouseY) > 0.1) {
            animationFrame = requestAnimationFrame(updateLighting);
        }
    }

    /**
     * Track mouse movement for lighting effects
     */
    function handleMouseMove(e) {
        mouseX = (e.clientX / window.innerWidth) * 100;
        mouseY = (e.clientY / window.innerHeight) * 100;

        if (!animationFrame) {
            animationFrame = requestAnimationFrame(updateLighting);
        }
    }

    /**
     * Add hover lighting effects to glass elements
     */
    function addGlassInteractivity() {
        const glassElements = document.querySelectorAll('.liquid-glass, .glass, .rz-card');
        
        glassElements.forEach(element => {
            element.addEventListener('mouseenter', function(e) {
                const rect = this.getBoundingClientRect();
                const x = ((e.clientX - rect.left) / rect.width) * 100;
                const y = ((e.clientY - rect.top) / rect.height) * 100;
                
                this.style.setProperty('--hover-x', `${x}%`);
                this.style.setProperty('--hover-y', `${y}%`);
            });

            element.addEventListener('mousemove', function(e) {
                const rect = this.getBoundingClientRect();
                const x = ((e.clientX - rect.left) / rect.width) * 100;
                const y = ((e.clientY - rect.top) / rect.height) * 100;
                
                this.style.setProperty('--hover-x', `${x}%`);
                this.style.setProperty('--hover-y', `${y}%`);
            });
        });
    }

    /**
     * Parallax effect for depth layers
     */
    function addParallaxEffect() {
        window.addEventListener('scroll', function() {
            const scrolled = window.pageYOffset;
            const parallaxElements = document.querySelectorAll('[data-parallax]');
            
            parallaxElements.forEach(element => {
                const speed = element.dataset.parallax || 0.5;
                const yPos = -(scrolled * speed);
                element.style.transform = `translateY(${yPos}px)`;
            });
        });
    }

    /**
     * Add focus lighting for keyboard navigation (accessibility)
     */
    function addFocusLighting() {
        document.addEventListener('focusin', function(e) {
            if (e.target.matches('button, a, input, textarea, select, [tabindex]')) {
                const rect = e.target.getBoundingClientRect();
                mouseX = ((rect.left + rect.width / 2) / window.innerWidth) * 100;
                mouseY = ((rect.top + rect.height / 2) / window.innerHeight) * 100;
                
                if (!animationFrame) {
                    animationFrame = requestAnimationFrame(updateLighting);
                }
            }
        });
    }

    /**
     * Add tilt effect to cards for spatial computing feel
     */
    function addTiltEffect() {
        const cards = document.querySelectorAll('.rz-card, .liquid-glass');
        
        cards.forEach(card => {
            card.addEventListener('mousemove', function(e) {
                const rect = this.getBoundingClientRect();
                const x = e.clientX - rect.left;
                const y = e.clientY - rect.top;
                
                const centerX = rect.width / 2;
                const centerY = rect.height / 2;
                
                const rotateX = ((y - centerY) / centerY) * -5; // Max 5deg rotation
                const rotateY = ((x - centerX) / centerX) * 5;
                
                this.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) translateY(-2px)`;
            });
            
            card.addEventListener('mouseleave', function() {
                this.style.transform = '';
            });
        });
    }

    /**
     * Intersection Observer for scroll-based animations
     */
    function addScrollAnimations() {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('fade-in');
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: '0px 0px -100px 0px'
        });

        // Observe all bento grid cells
        document.querySelectorAll('[class*="bento-cell"]').forEach(cell => {
            observer.observe(cell);
        });
    }

    /**
     * Initialize all effects when DOM is ready
     */
    function init() {
        console.log('?? Liquid Glass 2026 initialized');
        
        // Core lighting system
        document.addEventListener('mousemove', handleMouseMove);
        
        // Interactive effects
        addGlassInteractivity();
        addParallaxEffect();
        addFocusLighting();
        addTiltEffect();
        addScrollAnimations();
        
        // Re-initialize on Blazor navigation
        if (window.Blazor) {
            window.Blazor.addEventListener('enhancedload', () => {
                setTimeout(() => {
                    addGlassInteractivity();
                    addTiltEffect();
                    addScrollAnimations();
                }, 100);
            });
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    /**
     * Performance monitoring (optional)
     */
    if (window.performance && window.performance.measure) {
        window.addEventListener('load', () => {
            const perfData = performance.getEntriesByType('navigation')[0];
            console.log('? Liquid Glass Performance:', {
                domContentLoaded: `${perfData.domContentLoadedEventEnd - perfData.domContentLoadedEventStart}ms`,
                loadComplete: `${perfData.loadEventEnd - perfData.loadEventStart}ms`
            });
        });
    }

    /**
     * Expose API for manual control
     */
    window.LiquidGlass = {
        refresh: function() {
            addGlassInteractivity();
            addTiltEffect();
            addScrollAnimations();
        },
        setLighting: function(x, y) {
            mouseX = x;
            mouseY = y;
            if (!animationFrame) {
                animationFrame = requestAnimationFrame(updateLighting);
            }
        }
    };

})();
