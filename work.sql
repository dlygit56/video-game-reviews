DROP TABLE IF EXISTS review_views, ratings, reviews, game_genres, games, genres, users, review_statuses, roles CASCADE;
DROP DOMAIN IF EXISTS d_email, d_username, d_score CASCADE;


CREATE DOMAIN d_email AS VARCHAR(255)
    CONSTRAINT chk_email_format CHECK (VALUE ~* '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$');

CREATE DOMAIN d_username AS VARCHAR(64)
    CONSTRAINT chk_username_format CHECK (VALUE ~ '^[A-Za-z0-9_.-]{3,64}$');

CREATE DOMAIN d_score AS SMALLINT
    CONSTRAINT chk_score_range CHECK (VALUE BETWEEN 1 AND 10);


CREATE TABLE roles(
    id SMALLINT    PRIMARY KEY,
    code VARCHAR(16) NOT NULL UNIQUE,
    name VARCHAR(64) NOT NULL
);

CREATE TABLE review_statuses (
    id   SMALLINT    PRIMARY KEY,
    code VARCHAR(16) NOT NULL UNIQUE,
    name VARCHAR(64) NOT NULL
);

CREATE TABLE genres (
    id   SMALLINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name VARCHAR(64) NOT NULL UNIQUE
);

INSERT INTO roles(id, code, name) VALUES
    (1, 'user',   'Пользователь'),
    (2, 'critic', 'Критик'),
    (3, 'admin', 'Администратор');

INSERT INTO review_statuses(id, code, name) VALUES
    (1, 'draft',     'Черновик'),
    (2, 'published', 'Опубликована'),
    (3, 'hidden', 'Скрыта');

INSERT INTO genres(name) VALUES
    ('Action'), ('RPG'), ('Strategy'), ('Adventure'),
    ('Simulation'), ('Sports'), ('Racing'), ('Puzzle'), ('Horror');


CREATE TABLE users(
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username d_username   NOT NULL,
    email d_email      NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role_id       SMALLINT     NOT NULL DEFAULT 1
                  REFERENCES roles(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_users_username_lower ON users (lower(username));
CREATE UNIQUE INDEX ux_users_email_lower    ON users (lower(email));
CREATE INDEX idx_users_role ON users(role_id);

CREATE TABLE games (
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title        VARCHAR(255) NOT NULL CHECK (length(btrim(title)) > 0),
    description  TEXT,
    release_date DATE CHECK (release_date >= DATE '1970-01-01'),
    -- NUMERIC(4,2), т.к. в (3,2) максимум 9.99 и значение 10.00 не поместится
    rating_avg   NUMERIC(4,2) NOT NULL DEFAULT 0 CHECK (rating_avg BETWEEN 0 AND 10),
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT uq_games_title_release UNIQUE (title, release_date)
);

CREATE TABLE game_genres (
    game_id  BIGINT   NOT NULL REFERENCES games(id)  ON DELETE CASCADE,
    genre_id SMALLINT NOT NULL REFERENCES genres(id) ON DELETE RESTRICT,
    PRIMARY KEY (game_id, genre_id)
);
CREATE INDEX idx_game_genres_genre ON game_genres(genre_id);

CREATE TABLE reviews (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    critic_id  BIGINT   NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id    BIGINT   NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    title      VARCHAR(255),
    body       TEXT     NOT NULL CHECK (length(btrim(body)) > 0),
    status_id  SMALLINT NOT NULL DEFAULT 1
               REFERENCES review_statuses(id) ON UPDATE CASCADE ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- один критик — одна рецензия на игру
    CONSTRAINT uq_reviews_critic_game UNIQUE (critic_id, game_id)
);
CREATE INDEX idx_reviews_game   ON reviews(game_id);
CREATE INDEX idx_reviews_status ON reviews(status_id);

CREATE TABLE ratings (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    review_id  BIGINT  NOT NULL UNIQUE REFERENCES reviews(id) ON DELETE CASCADE,
    score      d_score NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE review_views (
    user_id   BIGINT NOT NULL REFERENCES users(id)   ON DELETE CASCADE,
    review_id BIGINT NOT NULL REFERENCES reviews(id) ON DELETE CASCADE,
    viewed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, review_id)
);
CREATE INDEX idx_views_review ON review_views(review_id);


CREATE OR REPLACE FUNCTION trg_set_updated_at() RETURNS trigger AS $$
BEGIN
    NEW.updated_at := now();
RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_reviews_updated_at
    BEFORE UPDATE ON reviews
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

CREATE OR REPLACE FUNCTION trg_reviews_check_author() RETURNS trigger AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM users WHERE id = NEW.critic_id AND role_id = 2) THEN
        RAISE EXCEPTION 'Рецензию может написать только пользователь с ролью "Критик"'
            USING ERRCODE = 'check_violation';
END IF;
RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_reviews_author
    BEFORE INSERT OR UPDATE OF critic_id ON reviews
    FOR EACH ROW EXECUTE FUNCTION trg_reviews_check_author();

CREATE OR REPLACE FUNCTION recalc_game_rating(p_game BIGINT) RETURNS void AS $$
    UPDATE games
       SET rating_avg = COALESCE((
            SELECT ROUND(AVG(r.score), 2)
              FROM ratings r
              JOIN reviews v ON v.id = r.review_id
             WHERE v.game_id = p_game AND v.status_id = 2), 0)
     WHERE id = p_game;
$$ LANGUAGE sql;

CREATE OR REPLACE FUNCTION trg_ratings_recalc() RETURNS trigger AS $$
DECLARE g BIGINT;
BEGIN
    IF TG_OP IN ('UPDATE', 'DELETE') THEN
        SELECT game_id INTO g FROM reviews WHERE id = OLD.review_id;
IF g IS NOT NULL THEN PERFORM recalc_game_rating(g); END IF;
END IF;
IF TG_OP IN ('INSERT', 'UPDATE') THEN
        SELECT game_id INTO g FROM reviews WHERE id = NEW.review_id;
IF g IS NOT NULL THEN PERFORM recalc_game_rating(g); END IF;
END IF;
RETURN NULL;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_ratings_avg
    AFTER INSERT OR UPDATE OR DELETE ON ratings
    FOR EACH ROW EXECUTE FUNCTION trg_ratings_recalc();

CREATE OR REPLACE FUNCTION trg_reviews_recalc() RETURNS trigger AS $$
BEGIN
    PERFORM recalc_game_rating(OLD.game_id);
IF TG_OP = 'UPDATE' AND NEW.game_id <> OLD.game_id THEN
        PERFORM recalc_game_rating(NEW.game_id);
END IF;
RETURN NULL;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_reviews_avg
    AFTER UPDATE OF status_id, game_id OR DELETE ON reviews
    FOR EACH ROW EXECUTE FUNCTION trg_reviews_recalc();