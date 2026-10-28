CREATE TABLE users (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username      VARCHAR(64)  NOT NULL UNIQUE,
    email         VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role          VARCHAR(16)  NOT NULL DEFAULT 'user'
                  CHECK (role IN ('user', 'critic', 'admin')),
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE games (
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title        VARCHAR(255) NOT NULL,
    description  TEXT,
    release_date DATE,
    rating_avg   NUMERIC(3,2) NOT NULL DEFAULT 0 CHECK (rating_avg BETWEEN 0 AND 10),
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE reviews (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    critic_id  BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title      VARCHAR(255),
    body       TEXT   NOT NULL,
    status     VARCHAR(16) NOT NULL DEFAULT 'published' CHECK (status IN ('draft', 'published', 'hidden')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_reviews_critic ON reviews(critic_id);

CREATE TABLE ratings (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    review_id  BIGINT   NOT NULL UNIQUE REFERENCES reviews(id) ON DELETE CASCADE,
    game_id    BIGINT   NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    score      SMALLINT NOT NULL CHECK (score BETWEEN 1 AND 10),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_ratings_game ON ratings(game_id);

CREATE TABLE review_views (
    user_id    BIGINT NOT NULL REFERENCES users(id)   ON DELETE CASCADE,
    review_id  BIGINT NOT NULL REFERENCES reviews(id) ON DELETE CASCADE,
    viewed_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, review_id)
);

CREATE INDEX idx_views_review ON review_views(review_id);