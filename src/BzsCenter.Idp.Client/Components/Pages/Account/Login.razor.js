const trackingStates = new WeakMap();

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

function lerp(a, b, t) {
    return a + (b - a) * t;
}

function getGsap() {
    if (!window.gsap) {
        throw new Error("GSAP is not loaded. Ensure lib/gsap/gsap.min.js is loaded before Login.razor.js import.");
    }
    return window.gsap;
}

// Per-eye polar coordinate tracking with lerp smoothing.
// Each pupil/dot calculates its own atan2 from its screen-space center.
function updatePupils(heroElement, state) {
    const pupils = heroElement.querySelectorAll(".pupil, .dot");
    const mouseX = state.mouseX;
    const mouseY = state.mouseY;
    const MAX_DISTANCE = 5;
    // Lerp factor per frame: ~0.08 at 60fps ≈ ~150ms settle time (smooth but responsive)
    const LERP_T = 0.08;

    pupils.forEach((pupil) => {
        const parent = pupil.parentElement; // .eye or .eyes (for .dot)
        if (!parent) return;

        const rect = parent.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;

        let targetX, targetY;

        if (state.isPasswordFocus) {
            // Password mode: pupils hidden (eyes closed via GSAP), just park pupils center
            targetX = 0;
            targetY = 0;
        } else if (state.isShowPasswordState) {
            const char = pupil.closest(".character");
            if (char && char.classList.contains("purple")) {
                targetX = -MAX_DISTANCE;
                targetY = -MAX_DISTANCE;
            } else {
                targetX = MAX_DISTANCE * 0.6;
                targetY = -MAX_DISTANCE * 0.4;
            }
        } else if (state.isLookingAtEachOther) {
            const char = pupil.closest(".character");
            if (char) {
                if (char.classList.contains("purple")) {
                    targetX = MAX_DISTANCE;
                    targetY = 0;
                } else if (char.classList.contains("charcoal")) {
                    targetX = -MAX_DISTANCE;
                    targetY = 0;
                } else {
                    const deltaX = mouseX - centerX;
                    const deltaY = mouseY - centerY;
                    const dist = Math.min(Math.sqrt(deltaX * deltaX + deltaY * deltaY), MAX_DISTANCE);
                    const angle = Math.atan2(deltaY, deltaX);
                    targetX = Math.cos(angle) * dist;
                    targetY = Math.sin(angle) * dist;
                }
            } else {
                targetX = 0;
                targetY = 0;
            }
        } else {
            // Normal mouse tracking: per-eye polar coordinate calculation
            const deltaX = mouseX - centerX;
            const deltaY = mouseY - centerY;
            const dist = Math.min(Math.sqrt(deltaX * deltaX + deltaY * deltaY), MAX_DISTANCE);
            const angle = Math.atan2(deltaY, deltaX);
            targetX = Math.cos(angle) * dist;
            targetY = Math.sin(angle) * dist;
        }

        // Lerp from current position for smooth motion
        const cur = pupil._lerpPos || { x: 0, y: 0 };
        const nx = lerp(cur.x, targetX, LERP_T);
        const ny = lerp(cur.y, targetY, LERP_T);
        pupil._lerpPos = { x: nx, y: ny };

        pupil.style.transform = `translate(${nx}px, ${ny}px)`;
    });
}

function calculatePosition(element, mouseX, mouseY) {
    const rect = element.getBoundingClientRect();
    const centerX = rect.left + rect.width / 2;
    const centerY = rect.top + rect.height / 3;

    const deltaX = mouseX - centerX;
    const deltaY = mouseY - centerY;

    return {
        faceX: clamp(deltaX / 20, -15, 15),
        faceY: clamp(deltaY / 30, -10, 10),
        bodySkew: clamp(-deltaX / 120, -6, 6),
    };
}

function setCharacterMotion(heroElement, state) {
    const purple = heroElement.querySelector(".character.purple");
    const charcoal = heroElement.querySelector(".character.charcoal");
    const orange = heroElement.querySelector(".character.orange");
    const yellow = heroElement.querySelector(".character.yellow");
    const purpleEyes = heroElement.querySelector(".character.purple .eyes");
    const charcoalEyes = heroElement.querySelector(".character.charcoal .eyes");
    const orangeEyes = heroElement.querySelector(".character.orange .eyes");
    const yellowEyes = heroElement.querySelector(".character.yellow .eyes");
    const mouth = heroElement.querySelector(".character.yellow .mouth");

    if (!purple || !charcoal || !orange || !yellow ||
        !purpleEyes || !charcoalEyes || !orangeEyes || !yellowEyes || !mouth) {
        return;
    }

    const purplePos = calculatePosition(purple, state.mouseX, state.mouseY);
    const charcoalPos = calculatePosition(charcoal, state.mouseX, state.mouseY);
    const orangePos = calculatePosition(orange, state.mouseX, state.mouseY);
    const yellowPos = calculatePosition(yellow, state.mouseX, state.mouseY);

    // ── Body transforms ──────────────────────────────────────────────────────
    purple.style.height = (state.isTyping || state.isHidingPassword || state.isPasswordFocus) ? "440px" : "400px";

    if (state.isPasswordFocus) {
        // Turn away: all characters rotate/skew hard to the side
        purple.style.transform = "skewX(-18deg) translateX(-30px)";
        charcoal.style.transform = "skewX(-14deg) translateX(-20px)";
        orange.style.transform = "skewX(-10deg) translateX(-10px)";
        yellow.style.transform = "skewX(-10deg) translateX(-10px)";
    } else if (state.isShowPasswordState) {
        purple.style.transform = "skewX(0deg)";
        charcoal.style.transform = "skewX(0deg)";
        orange.style.transform = "skewX(0deg)";
        yellow.style.transform = "skewX(0deg)";
    } else if (state.isTyping || state.isHidingPassword) {
        purple.style.transform = `skewX(${purplePos.bodySkew - 12}deg) translateX(40px)`;
        charcoal.style.transform = `skewX(${(charcoalPos.bodySkew * 1.5) + 10}deg) translateX(20px)`;
        orange.style.transform = `skewX(${orangePos.bodySkew}deg)`;
        yellow.style.transform = `skewX(${yellowPos.bodySkew}deg)`;
    } else {
        purple.style.transform = `skewX(${purplePos.bodySkew}deg)`;
        charcoal.style.transform = `skewX(${charcoalPos.bodySkew}deg)`;
        orange.style.transform = `skewX(${orangePos.bodySkew}deg)`;
        yellow.style.transform = `skewX(${yellowPos.bodySkew}deg)`;
    }

    // ── Eyes container position ───────────────────────────────────────────────
    if (state.isPasswordFocus) {
        // Eyes slide off to the side (turn-away pose)
        purpleEyes.style.left = "10px";
        purpleEyes.style.top = "40px";
        charcoalEyes.style.left = "6px";
        charcoalEyes.style.top = "32px";
        orangeEyes.style.left = "60px";
        orangeEyes.style.top = "90px";
        yellowEyes.style.left = "35px";
        yellowEyes.style.top = "40px";
        mouth.style.left = "25px";
        mouth.style.top = "88px";
        mouth.style.height = "4px";
        mouth.style.borderRadius = "999px";
    } else if (state.isShowPasswordState) {
        purpleEyes.style.left = "20px";
        purpleEyes.style.top = "35px";
        charcoalEyes.style.left = "10px";
        charcoalEyes.style.top = "28px";
        orangeEyes.style.left = "50px";
        orangeEyes.style.top = "85px";
        yellowEyes.style.left = "20px";
        yellowEyes.style.top = "35px";
        mouth.style.left = "10px";
        mouth.style.top = "88px";
        mouth.style.height = "4px";
        mouth.style.borderRadius = "999px";
    } else {
        purpleEyes.style.left = `${state.isLookingAtEachOther ? 55 : (45 + purplePos.faceX)}px`;
        purpleEyes.style.top = `${state.isLookingAtEachOther ? 65 : (40 + purplePos.faceY)}px`;
        charcoalEyes.style.left = `${state.isLookingAtEachOther ? 32 : (26 + charcoalPos.faceX)}px`;
        charcoalEyes.style.top = `${state.isLookingAtEachOther ? 12 : (32 + charcoalPos.faceY)}px`;
        orangeEyes.style.left = `${82 + orangePos.faceX}px`;
        orangeEyes.style.top = `${90 + orangePos.faceY}px`;
        yellowEyes.style.left = `${52 + yellowPos.faceX}px`;
        yellowEyes.style.top = `${40 + yellowPos.faceY}px`;
        mouth.style.left = `${40 + yellowPos.faceX}px`;
        mouth.style.top = `${88 + yellowPos.faceY}px`;

        if (state.isTyping) {
            mouth.style.height = "18px";
            mouth.style.borderRadius = "30px";
        } else {
            mouth.style.height = "4px";
            mouth.style.borderRadius = "999px";
        }
    }
}

function deriveInputState(state) {
    const usernameInput = state.usernameInput;
    const passwordInput = state.passwordInput;

    const isTyping = !!(usernameInput && document.activeElement === usernameInput);
    const isPasswordFocus = !!(passwordInput && document.activeElement === passwordInput);
    const passwordLength = passwordInput ? passwordInput.value.length : 0;
    const showPassword = !!(passwordInput && passwordInput.type === "text");
    const isHidingPassword = passwordLength > 0 && !showPassword && !isPasswordFocus;
    const isShowPasswordState = passwordLength > 0 && showPassword && !isPasswordFocus;

    state.isTyping = isTyping;
    state.isPasswordFocus = isPasswordFocus;
    state.isHidingPassword = isHidingPassword;
    state.isShowPasswordState = isShowPasswordState;
}

// Close all eyes of a character (for password-focus state)
function closeEyes(heroElement, selector, state) {
    if (state._eyesClosedForPassword) return;
    state._eyesClosedForPassword = true;

    const gsap = getGsap();
    const eyes = heroElement.querySelectorAll(selector);
    eyes.forEach((eye) => {
        const normalHeight = parseFloat(eye.dataset.normalHeight) || parseFloat(getComputedStyle(eye).height) || 18;
        eye.dataset.normalHeight = normalHeight;
        // Squint to ~2px (visible slit, not invisible) — "眯眼" effect
        gsap.to(eye, { height: 2, duration: 0.25, ease: "power2.inOut", overwrite: true });
    });
}

function openEyes(heroElement, selector, state) {
    if (!state._eyesClosedForPassword) return;
    state._eyesClosedForPassword = false;

    const gsap = getGsap();
    const eyes = heroElement.querySelectorAll(selector);
    eyes.forEach((eye) => {
        const normalHeight = parseFloat(eye.dataset.normalHeight) || 18;
        gsap.to(eye, { height: normalHeight, duration: 0.3, ease: "power2.out", overwrite: true });
    });
}

function scheduleRandomBlink(heroElement, selector, state) {
    const gsap = getGsap();

    // Pre-initialize inline height so GSAP can tween from a known value
    const eyes = heroElement.querySelectorAll(selector);
    eyes.forEach((eye) => {
        const normalHeight = parseFloat(getComputedStyle(eye).height);
        eye.dataset.normalHeight = normalHeight;
        gsap.set(eye, { height: normalHeight });
    });

    function blink() {
        if (!trackingStates.has(heroElement)) return; // disposed
        // Don't blink while eyes are closed for password
        if (state._eyesClosedForPassword) {
            const nextDelay = Math.random() * 4000 + 3000;
            const timer = window.setTimeout(blink, nextDelay);
            state.blinkTimers.push(timer);
            return;
        }

        const currentEyes = heroElement.querySelectorAll(selector);
        if (currentEyes.length === 0) return;

        currentEyes.forEach((eye) => {
            const normalHeight = parseFloat(eye.dataset.normalHeight) || 18;
            gsap.timeline()
                .to(eye, { height: 2, duration: 0.075, ease: "power2.in", overwrite: true })
                .to(eye, { height: normalHeight, duration: 0.075, ease: "power2.out" });
        });

        const nextDelay = Math.random() * 4000 + 3000;
        const timer = window.setTimeout(blink, nextDelay);
        state.blinkTimers.push(timer);
    }

    const initialDelay = Math.random() * 3000 + 1000;
    const timer = window.setTimeout(blink, initialDelay);
    state.blinkTimers.push(timer);
}

function cleanupHeroTracking(heroElement, state) {
    if (!state) return;

    if (state.observer) {
        state.observer.disconnect();
        state.observer = null;
    }

    if (state.tickerFn) {
        getGsap().ticker.remove(state.tickerFn);
        state.tickerFn = null;
    }

    if (state.moveHandler) {
        window.removeEventListener("mousemove", state.moveHandler);
        state.moveHandler = null;
    }

    state.inputHandlers.forEach(({ el, type, fn }) => el.removeEventListener(type, fn));
    state.inputHandlers = [];

    state.blinkTimers.forEach((t) => window.clearTimeout(t));
    state.blinkTimers = [];

    if (state._lookingTimer) {
        window.clearTimeout(state._lookingTimer);
        state._lookingTimer = null;
    }

    trackingStates.delete(heroElement);
}

function createCleanupObserver(heroElement, state) {
    if (!document.body) return;
    const observer = new MutationObserver(() => {
        if (!document.body.contains(heroElement)) {
            cleanupHeroTracking(heroElement, state);
        }
    });
    observer.observe(document.body, { childList: true, subtree: true });
    state.observer = observer;
}

export function initHeroTracking(heroElement) {
    if (!heroElement || trackingStates.has(heroElement)) return;

    const gsap = getGsap();

    const usernameInput = document.getElementById("username");
    const passwordInput = document.getElementById("password");

    const state = {
        mouseX: window.innerWidth / 2,
        mouseY: window.innerHeight / 2,
        isTyping: false,
        isPasswordFocus: false,
        isHidingPassword: false,
        isShowPasswordState: false,
        isLookingAtEachOther: false,
        _lookingTimerPending: false,
        _lookingTimer: null,
        _eyesClosedForPassword: false,
        blinkTimers: [],
        moveHandler: null,
        inputHandlers: [],
        tickerFn: null,
        usernameInput: (usernameInput instanceof HTMLInputElement) ? usernameInput : null,
        passwordInput: (passwordInput instanceof HTMLInputElement) ? passwordInput : null,
        observer: null,
    };

    createCleanupObserver(heroElement, state);

    // GSAP ticker: every frame drives pupil lerp + body/eye container updates
    state.tickerFn = () => {
        deriveInputState(state);
        setCharacterMotion(heroElement, state);
        updatePupils(heroElement, state);
    };
    gsap.ticker.add(state.tickerFn);

    // mousemove: coordinate store only
    state.moveHandler = (event) => {
        state.mouseX = event.clientX;
        state.mouseY = event.clientY;
    };
    window.addEventListener("mousemove", state.moveHandler, { passive: true });

    // Username focus → isLookingAtEachOther for 800ms
    if (state.usernameInput) {
        const onUsernameFocus = () => {
            if (state._lookingTimer) window.clearTimeout(state._lookingTimer);
            state.isLookingAtEachOther = true;
            state._lookingTimerPending = true;
            state._lookingTimer = window.setTimeout(() => {
                state.isLookingAtEachOther = false;
                state._lookingTimerPending = false;
                state._lookingTimer = null;
            }, 800);
        };
        state.usernameInput.addEventListener("focus", onUsernameFocus);
        state.usernameInput.addEventListener("input", onUsernameFocus);
        state.inputHandlers.push({ el: state.usernameInput, type: "focus", fn: onUsernameFocus });
        state.inputHandlers.push({ el: state.usernameInput, type: "input", fn: onUsernameFocus });
    }

    // Password focus/blur → close/open all eyes
    if (state.passwordInput) {
        const ALL_EYES = ".character.purple .eye, .character.charcoal .eye";

        const onPasswordFocus = () => {
            closeEyes(heroElement, ALL_EYES, state);
        };
        const onPasswordBlur = () => {
            openEyes(heroElement, ALL_EYES, state);
        };

        state.passwordInput.addEventListener("focus", onPasswordFocus);
        state.passwordInput.addEventListener("blur", onPasswordBlur);
        state.inputHandlers.push({ el: state.passwordInput, type: "focus", fn: onPasswordFocus });
        state.inputHandlers.push({ el: state.passwordInput, type: "blur", fn: onPasswordBlur });
    }

    // Random blinking for purple and charcoal
    scheduleRandomBlink(heroElement, ".character.purple .eye", state);
    scheduleRandomBlink(heroElement, ".character.charcoal .eye", state);

    // Initial state
    deriveInputState(state);
    setCharacterMotion(heroElement, state);
    updatePupils(heroElement, state);

    trackingStates.set(heroElement, state);
}

export function disposeHeroTracking(heroElement) {
    const state = trackingStates.get(heroElement);
    cleanupHeroTracking(heroElement, state);
}
